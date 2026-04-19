import asyncio
import sys
import tempfile
import threading
import time
import unittest
import uuid
import warnings
import zipfile
from pathlib import Path
from unittest.mock import patch

from watchfiles import Change


REPO_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(REPO_ROOT / "worker"))


def write_docx_with_nested_archive(path: Path):
    with zipfile.ZipFile(path, "w") as archive:
        archive.writestr(
            "[Content_Types].xml",
            """<?xml version="1.0" encoding="UTF-8"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Override PartName="/word/document.xml"
    ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
</Types>""",
        )
        archive.writestr(
            "_rels/.rels",
            """<?xml version="1.0" encoding="UTF-8"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1"
    Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"
    Target="word/document.xml"/>
</Relationships>""",
        )
        archive.writestr(
            "word/document.xml",
            """<?xml version="1.0" encoding="UTF-8"?>
<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:body><w:p><w:r><w:t>Hello</w:t></w:r></w:p></w:body>
</w:document>""",
        )
        archive.writestr("word/embeddings/payload.zip", b"nested archive")


class WatcherAsyncFlowAndSafetyTests(unittest.IsolatedAsyncioTestCase):
    async def asyncSetUp(self):
        asyncio.get_running_loop().slow_callback_duration = 5
        warnings.filterwarnings(
            "ignore",
            message="The ``declarative_base\\(\\)`` function is now available",
            category=DeprecationWarning,
        )

    async def test_process_file_debounces_same_path_and_runs_admission_off_event_loop(self):
        import FileWatcher as watcher_module

        main_thread_id = threading.get_ident()
        call_threads = []

        def slow_sync_admission(path):
            call_threads.append(threading.get_ident())
            time.sleep(0.05)

        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / f"sample__{uuid.uuid4()}.txt"
            path.write_text("hello", encoding="utf-8")
            watcher = watcher_module.FileWatcher(Path(temp_dir))
            watcher._process_file_sync = slow_sync_admission

            await asyncio.gather(
                watcher.process_file(str(path)),
                watcher.process_file(str(path)),
            )

        self.assertEqual(len(call_threads), 1)
        self.assertNotEqual(call_threads[0], main_thread_id)

    async def test_handle_watch_changes_schedules_added_and_modified_events(self):
        import FileWatcher as watcher_module

        processed = []

        async def fake_process_file(file_path):
            processed.append(Path(file_path).name)

        with tempfile.TemporaryDirectory() as temp_dir:
            first = Path(temp_dir) / f"first__{uuid.uuid4()}.txt"
            second = Path(temp_dir) / f"second__{uuid.uuid4()}.txt"
            third = Path(temp_dir) / f"third__{uuid.uuid4()}.txt"
            watcher = watcher_module.FileWatcher(Path(temp_dir))
            watcher.process_file = fake_process_file

            await watcher.handle_watch_changes(
                {
                    (Change.added, str(first)),
                    (Change.modified, str(second)),
                    (Change.deleted, str(third)),
                }
            )
            await watcher.wait_for_processing_tasks()

        self.assertCountEqual(processed, [first.name, second.name])

    async def test_text_document_over_direct_text_limit_is_rejected(self):
        import FileWatcher as watcher_module

        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "large.txt"
            path.write_text("abcdef", encoding="utf-8")
            watcher = watcher_module.FileWatcher(Path(temp_dir))

            with patch.object(watcher_module, "MAX_TEXT_FILE_SIZE", 5, create=True):
                is_valid, failure_code, _ = watcher._validate_file_structure(path, ".txt")

        self.assertFalse(is_valid)
        self.assertEqual(failure_code, "TEXT_TOO_LARGE")

    async def test_pdf_over_page_limit_is_rejected(self):
        import FileWatcher as watcher_module

        class FakePdfReader:
            def __init__(self, path):
                self.pages = [object(), object()]
                self.is_encrypted = False

        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "two-pages.pdf"
            path.write_bytes(b"%PDF-1.7\n")
            watcher = watcher_module.FileWatcher(Path(temp_dir))

            with patch.object(watcher_module, "MAX_PDF_PAGES", 1, create=True):
                with patch.object(watcher_module, "PdfReader", FakePdfReader):
                    is_valid, failure_code, metadata = watcher._validate_file_structure(path, ".pdf")

        self.assertFalse(is_valid)
        self.assertEqual(failure_code, "PDF_TOO_MANY_PAGES")
        self.assertEqual(metadata["pages"], 2)

    async def test_docx_with_nested_archive_is_rejected(self):
        import FileWatcher as watcher_module

        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "nested.docx"
            write_docx_with_nested_archive(path)
            watcher = watcher_module.FileWatcher(Path(temp_dir))

            is_valid, failure_code, _ = watcher._validate_file_structure(path, ".docx")

        self.assertFalse(is_valid)
        self.assertEqual(failure_code, "NESTED_ARCHIVE_UNSUPPORTED")


if __name__ == "__main__":
    unittest.main()

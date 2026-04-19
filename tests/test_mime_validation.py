import asyncio
import sys
import tempfile
import unittest
import warnings
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(REPO_ROOT / "worker"))


class MimeValidationTests(unittest.IsolatedAsyncioTestCase):
    async def asyncSetUp(self):
        asyncio.get_running_loop().slow_callback_duration = 5
        warnings.filterwarnings(
            "ignore",
            message="The ``declarative_base\\(\\)`` function is now available",
            category=DeprecationWarning,
        )

    async def test_pdf_extension_rejects_text_content_as_mime_mismatch(self):
        import FileWatcher as watcher_module

        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "spoofed.pdf"
            path.write_text("this is just text", encoding="utf-8")
            watcher = watcher_module.FileWatcher(Path(temp_dir))

            is_valid, failure_code, detected_mime, source = await watcher.validate_extension_mime(
                path,
                ".pdf",
                None,
            )

        self.assertFalse(is_valid)
        self.assertEqual(failure_code, "MIME_MISMATCH")
        self.assertEqual(detected_mime, "text/plain")
        self.assertEqual(source, "text")

    async def test_pdf_extension_accepts_pdf_signature(self):
        import FileWatcher as watcher_module

        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "document.pdf"
            path.write_bytes(b"%PDF-1.7\n% test\n")
            watcher = watcher_module.FileWatcher(Path(temp_dir))

            is_valid, failure_code, detected_mime, source = await watcher.validate_extension_mime(
                path,
                ".pdf",
                "application/pdf",
            )

        self.assertTrue(is_valid)
        self.assertEqual(failure_code, "")
        self.assertEqual(detected_mime, "application/pdf")
        self.assertEqual(source, "sniffed")

    async def test_html_extension_rejects_text_that_does_not_look_like_html(self):
        import FileWatcher as watcher_module

        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "notes.html"
            path.write_text("plain notes without tags", encoding="utf-8")
            watcher = watcher_module.FileWatcher(Path(temp_dir))

            is_valid, failure_code, detected_mime, source = await watcher.validate_extension_mime(
                path,
                ".html",
                None,
            )

        self.assertFalse(is_valid)
        self.assertEqual(failure_code, "MIME_MISMATCH")
        self.assertEqual(detected_mime, "text/plain")
        self.assertEqual(source, "text")

    async def test_text_extension_rejects_binary_signature(self):
        import FileWatcher as watcher_module

        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "payload.txt"
            path.write_bytes(b"\x89PNG\r\n\x1a\n")
            watcher = watcher_module.FileWatcher(Path(temp_dir))

            is_valid, failure_code, detected_mime, source = await watcher.validate_extension_mime(
                path,
                ".txt",
                "image/png",
            )

        self.assertFalse(is_valid)
        self.assertEqual(failure_code, "MIME_MISMATCH")
        self.assertEqual(detected_mime, "image/png")
        self.assertEqual(source, "sniffed")


if __name__ == "__main__":
    unittest.main()

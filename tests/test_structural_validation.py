import logging
import sys
import tempfile
import unittest
import warnings
import zipfile
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(REPO_ROOT / "worker"))


def write_minimal_docx(path: Path):
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


def write_minimal_odt(path: Path):
    with zipfile.ZipFile(path, "w") as archive:
        archive.writestr("mimetype", "application/vnd.oasis.opendocument.text")
        archive.writestr(
            "content.xml",
            """<?xml version="1.0" encoding="UTF-8"?>
<office:document-content
  xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
  xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0">
  <office:body><office:text><text:p>Hello</text:p></office:text></office:body>
</office:document-content>""",
        )


class StructuralValidationTests(unittest.TestCase):
    def setUp(self):
        logging.getLogger("pypdf").setLevel(logging.CRITICAL)
        logging.getLogger("pypdf._reader").setLevel(logging.CRITICAL)
        warnings.filterwarnings(
            "ignore",
            message="The ``declarative_base\\(\\)`` function is now available",
            category=DeprecationWarning,
        )

    def test_corrupt_pdf_rejects_with_corrupt_pdf_code(self):
        import FileWatcher as watcher_module

        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "bad.pdf"
            path.write_bytes(b"%PDF-1.7\nnot a real pdf")
            watcher = watcher_module.FileWatcher(Path(temp_dir))

            is_valid, failure_code, _ = watcher._validate_file_structure(path, ".pdf")

        self.assertFalse(is_valid)
        self.assertEqual(failure_code, "CORRUPT_PDF")

    def test_text_file_rejects_whitespace_only_content(self):
        import FileWatcher as watcher_module

        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "blank.txt"
            path.write_text(" \r\n\t ", encoding="utf-8")
            watcher = watcher_module.FileWatcher(Path(temp_dir))

            is_valid, failure_code, _ = watcher._validate_file_structure(path, ".txt")

        self.assertFalse(is_valid)
        self.assertEqual(failure_code, "FILE_EMPTY")

    def test_json_file_rejects_invalid_json(self):
        import FileWatcher as watcher_module

        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "bad.json"
            path.write_text("{ not valid json }", encoding="utf-8")
            watcher = watcher_module.FileWatcher(Path(temp_dir))

            is_valid, failure_code, _ = watcher._validate_file_structure(path, ".json")

        self.assertFalse(is_valid)
        self.assertEqual(failure_code, "JSON_INVALID")

    def test_json_file_accepts_valid_json(self):
        import FileWatcher as watcher_module

        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "good.json"
            path.write_text('{"answer": 42}', encoding="utf-8")
            watcher = watcher_module.FileWatcher(Path(temp_dir))

            is_valid, failure_code, metadata = watcher._validate_file_structure(path, ".json")

        self.assertTrue(is_valid)
        self.assertEqual(failure_code, "")
        self.assertEqual(metadata["encoding"], "utf-8-sig")

    def test_docx_rejects_zip_without_core_office_structure(self):
        import FileWatcher as watcher_module

        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "bad.docx"
            with zipfile.ZipFile(path, "w") as archive:
                archive.writestr("not-word/document.xml", "<xml />")
            watcher = watcher_module.FileWatcher(Path(temp_dir))

            is_valid, failure_code, _ = watcher._validate_file_structure(path, ".docx")

        self.assertFalse(is_valid)
        self.assertEqual(failure_code, "ZIP_INVALID")

    def test_docx_accepts_minimal_core_office_structure(self):
        import FileWatcher as watcher_module

        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "good.docx"
            write_minimal_docx(path)
            watcher = watcher_module.FileWatcher(Path(temp_dir))

            is_valid, failure_code, metadata = watcher._validate_file_structure(path, ".docx")

        self.assertTrue(is_valid)
        self.assertEqual(failure_code, "")
        self.assertFalse(metadata["is_text"])

    def test_odt_rejects_zip_without_content_xml(self):
        import FileWatcher as watcher_module

        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "bad.odt"
            with zipfile.ZipFile(path, "w") as archive:
                archive.writestr("mimetype", "application/vnd.oasis.opendocument.text")
            watcher = watcher_module.FileWatcher(Path(temp_dir))

            is_valid, failure_code, _ = watcher._validate_file_structure(path, ".odt")

        self.assertFalse(is_valid)
        self.assertEqual(failure_code, "ZIP_INVALID")

    def test_odt_accepts_minimal_content_xml(self):
        import FileWatcher as watcher_module

        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "good.odt"
            write_minimal_odt(path)
            watcher = watcher_module.FileWatcher(Path(temp_dir))

            is_valid, failure_code, metadata = watcher._validate_file_structure(path, ".odt")

        self.assertTrue(is_valid)
        self.assertEqual(failure_code, "")
        self.assertFalse(metadata["is_text"])

    def test_doc_rejects_non_ole_content(self):
        import FileWatcher as watcher_module

        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "bad.doc"
            path.write_bytes(b"not an ole compound document")
            watcher = watcher_module.FileWatcher(Path(temp_dir))

            is_valid, failure_code, _ = watcher._validate_file_structure(path, ".doc")

        self.assertFalse(is_valid)
        self.assertEqual(failure_code, "STRUCTURAL_CORRUPTION")

    def test_rtf_rejects_missing_rtf_header(self):
        import FileWatcher as watcher_module

        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "bad.rtf"
            path.write_text("plain text", encoding="utf-8")
            watcher = watcher_module.FileWatcher(Path(temp_dir))

            is_valid, failure_code, _ = watcher._validate_file_structure(path, ".rtf")

        self.assertFalse(is_valid)
        self.assertEqual(failure_code, "STRUCTURAL_CORRUPTION")

    def test_csv_rejects_inconsistent_row_widths(self):
        import FileWatcher as watcher_module

        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "bad.csv"
            path.write_text("a,b\n1,2,3\n", encoding="utf-8")
            watcher = watcher_module.FileWatcher(Path(temp_dir))

            is_valid, failure_code, _ = watcher._validate_file_structure(path, ".csv")

        self.assertFalse(is_valid)
        self.assertEqual(failure_code, "CSV_INVALID")

    def test_html_accepts_non_empty_document_with_tags(self):
        import FileWatcher as watcher_module

        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "good.html"
            path.write_text("<html><body>Hello</body></html>", encoding="utf-8")
            watcher = watcher_module.FileWatcher(Path(temp_dir))

            is_valid, failure_code, metadata = watcher._validate_file_structure(path, ".html")

        self.assertTrue(is_valid)
        self.assertEqual(failure_code, "")
        self.assertEqual(metadata["encoding"], "utf-8-sig")


if __name__ == "__main__":
    unittest.main()

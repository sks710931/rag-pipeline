import asyncio
import csv
import io
import json
import os
import logging
import hashlib
import uuid
import filetype
import shutil
import time
import zipfile
import xml.etree.ElementTree as ET
from html.parser import HTMLParser
from pathlib import Path
from datetime import datetime
from dotenv import load_dotenv
from watchfiles import awatch, Change
from sqlalchemy.orm import Session
from sqlalchemy import update
from sqlalchemy.exc import IntegrityError
from pypdf import PdfReader

# Local imports
from database import SessionLocal
from models import Upload, FileMetadata, FileIngestion

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger("Worker")

# Configuration from Env
load_dotenv()
WATCH_DIR = Path(os.getenv("WATCH_DIR", "./uploads")).resolve()
MAX_FILE_SIZE = int(os.getenv("MAX_FILE_SIZE", 50 * 1024 * 1024)) # Default 50MB
MAX_TEXT_FILE_SIZE = int(os.getenv("MAX_TEXT_FILE_SIZE", 10 * 1024 * 1024)) # Default 10MB
MAX_PDF_PAGES = int(os.getenv("MAX_PDF_PAGES", "1000"))
MAX_ZIP_ENTRIES = int(os.getenv("MAX_ZIP_ENTRIES", "1000"))
MAX_ZIP_UNCOMPRESSED_SIZE = int(os.getenv("MAX_ZIP_UNCOMPRESSED_SIZE", 200 * 1024 * 1024))
WATCH_EVENT_DEBOUNCE_SECONDS = float(os.getenv("WATCH_EVENT_DEBOUNCE_SECONDS", "0.1"))
ADMISSION_VERSION = "1.0"
STABILITY_CHECKS = int(os.getenv("FILE_STABILITY_CHECKS", "3"))
STABILITY_INTERVAL_SECONDS = float(os.getenv("FILE_STABILITY_INTERVAL_SECONDS", "1.0"))

FAILURE_MESSAGES = {
    "UNSUPPORTED_EXTENSION": "File extension is not supported for admission.",
    "MIME_MISMATCH": "Detected file content does not match the uploaded extension.",
    "FILE_EMPTY": "File is empty or contains no meaningful content.",
    "FILE_NOT_STABLE": "File was still changing when admission attempted to process it.",
    "CORRUPT_PDF": "PDF structure could not be parsed.",
    "ENCRYPTED_PDF": "Encrypted PDF files are not accepted by admission.",
    "ZIP_INVALID": "ZIP-based document structure is invalid or incomplete.",
    "HASH_COMPUTE_FAILED": "Binary hash could not be computed.",
    "DB_WRITE_FAILED": "Admission metadata could not be written to the database.",
    "DUPLICATE_BINARY": "An identical binary file has already been admitted.",
    "FILE_TOO_LARGE": "File exceeds the configured admission size limit.",
    "TEXT_TOO_LARGE": "Text-like file exceeds the configured direct text admission limit.",
    "PDF_TOO_MANY_PAGES": "PDF exceeds the configured admission page limit.",
    "ZIP_TOO_MANY_ENTRIES": "ZIP-based document contains too many entries.",
    "ZIP_BOMB_RISK": "ZIP-based document exceeds the configured uncompressed size limit.",
    "NESTED_ARCHIVE_UNSUPPORTED": "Nested archives are not supported for admission.",
    "STRUCTURAL_CORRUPTION": "File structure failed lightweight validation.",
    "TEXT_DECODE_FAILED": "Text file could not be decoded safely.",
    "JSON_INVALID": "JSON document is not syntactically valid.",
    "CSV_INVALID": "CSV or TSV document has inconsistent row structure.",
    "HTML_INVALID": "HTML document does not contain valid HTML structure.",
    "INTERNAL_ERROR": "Admission failed because of an internal worker error.",
}

SUPPORTED_EXTENSIONS = {
    '.txt', '.pdf', '.doc', '.docx', '.md', '.markdown',
    '.html', '.htm', '.rtf', '.odt', '.csv', '.tsv', '.json'
}

TEXT_EXTENSIONS = {'.txt', '.md', '.markdown', '.json', '.csv', '.tsv'}
HTML_EXTENSIONS = {'.html', '.htm'}
DIRECT_TEXT_EXTENSIONS = TEXT_EXTENSIONS | HTML_EXTENSIONS | {'.rtf'}
NESTED_ARCHIVE_SUFFIXES = {
    '.zip', '.7z', '.rar', '.tar', '.gz', '.bz2', '.xz', '.tgz', '.tbz', '.txz'
}
SIGNATURE_MIME_BY_EXTENSION = {
    '.pdf': 'application/pdf',
    '.doc': 'application/msword',
    '.docx': 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
    '.odt': 'application/vnd.oasis.opendocument.text',
    '.rtf': 'application/rtf',
}
BINARY_MIME_EXTENSIONS = set(SIGNATURE_MIME_BY_EXTENSION)
OLE_COMPOUND_HEADER = bytes.fromhex("D0CF11E0A1B11AE1")

class HtmlStructureParser(HTMLParser):
    def __init__(self):
        super().__init__()
        self.has_starttag = False
        self.has_text = False

    def handle_starttag(self, tag, attrs):
        self.has_starttag = True

    def handle_data(self, data):
        if data.strip():
            self.has_text = True

class FileWatcher:
    def __init__(self, watch_dir: Path):
        self.watch_dir = Path(watch_dir).resolve()
        self.quarantine_dir = self.watch_dir / "quarantine"
        self.is_running = False
        self._inflight_paths: set[Path] = set()
        self._inflight_lock = asyncio.Lock()
        self._processing_tasks: set[asyncio.Task] = set()
        self._ensure_dirs()

    def _ensure_dirs(self):
        self.watch_dir.mkdir(parents=True, exist_ok=True)
        self.quarantine_dir.mkdir(parents=True, exist_ok=True)

    def _get_binary_hash(self, file_path: Path) -> str:
        """Synchronous hashing for use in to_thread."""
        sha256_hash = hashlib.sha256()
        with open(file_path, "rb") as f:
            for byte_block in iter(lambda: f.read(65536), b""):
                sha256_hash.update(byte_block)
        return sha256_hash.hexdigest()

    @staticmethod
    def _file_stability_signature(path: Path) -> tuple[int, int]:
        stat = path.stat()
        return stat.st_size, stat.st_mtime_ns

    async def wait_for_stable_file(
        self,
        path: Path,
        checks: int = STABILITY_CHECKS,
        interval_seconds: float = STABILITY_INTERVAL_SECONDS,
    ) -> bool:
        """Return True only when size and mtime stay unchanged across checks."""
        if checks < 1:
            checks = 1

        try:
            previous = await asyncio.to_thread(self._file_stability_signature, path)
        except OSError as e:
            logger.warning(f"Cannot read file state for {path.name}: {e}")
            return False

        for _ in range(checks):
            await asyncio.sleep(interval_seconds)
            try:
                current = await asyncio.to_thread(self._file_stability_signature, path)
            except OSError as e:
                logger.warning(f"Cannot re-check file state for {path.name}: {e}")
                return False

            if current != previous:
                logger.info(
                    f"File {path.name} is still changing "
                    f"(previous={previous}, current={current})"
                )
                return False
            previous = current

        return True

    def _wait_for_stable_file_sync(
        self,
        path: Path,
        checks: int = STABILITY_CHECKS,
        interval_seconds: float = STABILITY_INTERVAL_SECONDS,
    ) -> bool:
        """Thread-friendly version used by the blocking admission worker."""
        if checks < 1:
            checks = 1

        try:
            previous = self._file_stability_signature(path)
        except OSError as e:
            logger.warning(f"Cannot read file state for {path.name}: {e}")
            return False

        for _ in range(checks):
            time.sleep(interval_seconds)
            try:
                current = self._file_stability_signature(path)
            except OSError as e:
                logger.warning(f"Cannot re-check file state for {path.name}: {e}")
                return False

            if current != previous:
                logger.info(
                    f"File {path.name} is still changing "
                    f"(previous={previous}, current={current})"
                )
                return False
            previous = current

        return True

    @staticmethod
    def _read_prefix(path: Path, size: int = 4096) -> bytes:
        with open(path, "rb") as f:
            return f.read(size)

    @staticmethod
    def _decode_text_prefix(prefix: bytes) -> str | None:
        if b"\x00" in prefix:
            return None

        for encoding in ("utf-8-sig", "utf-8"):
            try:
                return prefix.decode(encoding)
            except UnicodeDecodeError:
                continue
        return None

    @staticmethod
    def _read_text_document(path: Path) -> tuple[str | None, str | None]:
        raw = path.read_bytes()
        if b"\x00" in raw:
            return None, None

        for encoding in ("utf-8-sig", "utf-8", "cp1252"):
            try:
                return raw.decode(encoding), encoding
            except UnicodeDecodeError:
                continue
        return None, None

    def _detect_signature_mime(self, path: Path, extension: str) -> tuple[str | None, str | None]:
        prefix = self._read_prefix(path)

        if extension == ".pdf":
            if prefix.startswith(b"%PDF-"):
                return "application/pdf", "signature"
            return None, None

        if extension == ".doc":
            if prefix.startswith(OLE_COMPOUND_HEADER):
                return "application/msword", "signature"
            return None, None

        if extension == ".rtf":
            if prefix.startswith(b"{\\rtf"):
                return "application/rtf", "signature"
            return None, None

        if extension in {".docx", ".odt"}:
            try:
                with zipfile.ZipFile(path) as archive:
                    names = set(archive.namelist())
                    if extension == ".docx":
                        if "[Content_Types].xml" in names and "word/document.xml" in names:
                            return (
                                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                                "signature",
                            )

                    if extension == ".odt" and "mimetype" in names:
                        mimetype = archive.read("mimetype").decode("ascii", errors="ignore")
                        if mimetype == "application/vnd.oasis.opendocument.text":
                            return mimetype, "signature"
            except (OSError, zipfile.BadZipFile):
                return None, None

        return None, None

    def _detect_text_mime(self, path: Path, extension: str) -> tuple[str | None, str | None]:
        prefix = self._read_prefix(path)
        text = self._decode_text_prefix(prefix)
        if text is None:
            return None, None

        if extension in HTML_EXTENSIONS:
            lower_text = text.lstrip().lower()
            html_markers = ("<!doctype html", "<html", "<head", "<body")
            if lower_text.startswith(html_markers) or any(marker in lower_text for marker in html_markers):
                return "text/html", "text"
            return "text/plain", "text"

        if extension == ".json":
            return "application/json", "text"

        if extension in TEXT_EXTENSIONS:
            return "text/plain", "text"

        return "text/plain", "text"

    def validate_extension_mime_sync(
        self,
        path: Path,
        extension: str,
        sniffed_mime: str | None,
    ) -> tuple[bool, str, str, str]:
        """Validate that detected content type belongs to the extension family."""
        extension = extension.lower()
        if extension not in SUPPORTED_EXTENSIONS:
            return False, "UNSUPPORTED_EXTENSION", "", ""

        if extension in BINARY_MIME_EXTENSIONS:
            signature_mime, signature_source = self._detect_signature_mime(path, extension)
            expected_mime = SIGNATURE_MIME_BY_EXTENSION[extension]
            if signature_mime == expected_mime:
                source = "sniffed" if sniffed_mime == expected_mime else signature_source or "signature"
                return True, "", signature_mime, source

            detected_mime = sniffed_mime
            source = "sniffed" if sniffed_mime else ""
            if not detected_mime:
                text_mime, text_source = self._detect_text_mime(path, extension)
                detected_mime = text_mime or "application/octet-stream"
                source = text_source or "unknown"

            return False, "MIME_MISMATCH", detected_mime, source

        if sniffed_mime and not sniffed_mime.startswith("text/") and sniffed_mime not in {
            "application/json",
            "application/xml",
            "text/csv",
            "text/tab-separated-values",
        }:
            return False, "MIME_MISMATCH", sniffed_mime, "sniffed"

        text_mime, text_source = self._detect_text_mime(path, extension)
        if not text_mime:
            return False, "MIME_MISMATCH", sniffed_mime or "application/octet-stream", (
                "sniffed" if sniffed_mime else "unknown"
            )

        if extension in HTML_EXTENSIONS and text_mime != "text/html":
            return False, "MIME_MISMATCH", text_mime, text_source or "text"

        return True, "", text_mime, text_source or ("sniffed" if sniffed_mime else "text")

    async def validate_extension_mime(
        self,
        path: Path,
        extension: str,
        sniffed_mime: str | None,
    ) -> tuple[bool, str, str, str]:
        return await asyncio.to_thread(
            self.validate_extension_mime_sync,
            path,
            extension,
            sniffed_mime,
        )

    def _validate_zip_safety(self, archive: zipfile.ZipFile) -> tuple[bool, str]:
        infos = archive.infolist()
        if len(infos) > MAX_ZIP_ENTRIES:
            return False, "ZIP_TOO_MANY_ENTRIES"

        total_uncompressed_size = 0
        for info in infos:
            total_uncompressed_size += max(info.file_size, 0)
            if total_uncompressed_size > MAX_ZIP_UNCOMPRESSED_SIZE:
                return False, "ZIP_BOMB_RISK"

            suffix = Path(info.filename).suffix.lower()
            if suffix in NESTED_ARCHIVE_SUFFIXES:
                return False, "NESTED_ARCHIVE_UNSUPPORTED"

        return True, ""

    def _validate_docx_structure(self, path: Path) -> tuple[bool, str]:
        try:
            with zipfile.ZipFile(path) as archive:
                is_safe, failure_code = self._validate_zip_safety(archive)
                if not is_safe:
                    return False, failure_code

                names = set(archive.namelist())
                required = {"[Content_Types].xml", "_rels/.rels", "word/document.xml"}
                if not required.issubset(names):
                    return False, "ZIP_INVALID"

                ET.fromstring(archive.read("[Content_Types].xml"))
                document_root = ET.fromstring(archive.read("word/document.xml"))
                if not document_root.tag.endswith("document"):
                    return False, "STRUCTURAL_CORRUPTION"
        except zipfile.BadZipFile:
            return False, "ZIP_INVALID"
        except (OSError, KeyError, ET.ParseError):
            return False, "STRUCTURAL_CORRUPTION"

        return True, ""

    def _validate_odt_structure(self, path: Path) -> tuple[bool, str]:
        try:
            with zipfile.ZipFile(path) as archive:
                is_safe, failure_code = self._validate_zip_safety(archive)
                if not is_safe:
                    return False, failure_code

                names = set(archive.namelist())
                if "mimetype" not in names or "content.xml" not in names:
                    return False, "ZIP_INVALID"

                mimetype = archive.read("mimetype").decode("ascii", errors="ignore")
                if mimetype != "application/vnd.oasis.opendocument.text":
                    return False, "STRUCTURAL_CORRUPTION"

                ET.fromstring(archive.read("content.xml"))
        except zipfile.BadZipFile:
            return False, "ZIP_INVALID"
        except (OSError, KeyError, ET.ParseError):
            return False, "STRUCTURAL_CORRUPTION"

        return True, ""

    def _validate_csv_structure(self, text: str, extension: str) -> tuple[bool, str]:
        delimiter = "\t" if extension == ".tsv" else ","
        rows = [
            row
            for row in csv.reader(io.StringIO(text), delimiter=delimiter)
            if any(cell.strip() for cell in row)
        ]
        if not rows:
            return False, "FILE_EMPTY"

        expected_width = len(rows[0])
        if expected_width == 0 or any(len(row) != expected_width for row in rows):
            return False, "CSV_INVALID"

        return True, ""

    def _validate_html_structure(self, text: str) -> tuple[bool, str]:
        parser = HtmlStructureParser()
        parser.feed(text)
        if not parser.has_starttag:
            return False, "HTML_INVALID"
        return True, ""

    def _validate_file_structure(self, path: Path, extension: str) -> tuple[bool, str, dict]:
        """Lightweight structural validation."""
        file_size = path.stat().st_size
        if file_size == 0:
            return False, "FILE_EMPTY", {}

        if file_size > MAX_FILE_SIZE:
            return False, "FILE_TOO_LARGE", {}

        if extension in DIRECT_TEXT_EXTENSIONS and file_size > MAX_TEXT_FILE_SIZE:
            return False, "TEXT_TOO_LARGE", {}

        metadata = {
            "pages": None,
            "is_encrypted": False,
            "is_text": True,
            "encoding": None,
        }

        try:
            if extension == '.pdf':
                if not self._read_prefix(path, 5).startswith(b"%PDF-"):
                    return False, "CORRUPT_PDF", metadata
                try:
                    reader = PdfReader(path)
                    metadata["pages"] = len(reader.pages)
                    metadata["is_encrypted"] = reader.is_encrypted
                    metadata["is_text"] = False
                    if reader.is_encrypted:
                        return False, "ENCRYPTED_PDF", metadata
                    if MAX_PDF_PAGES > 0 and metadata["pages"] > MAX_PDF_PAGES:
                        return False, "PDF_TOO_MANY_PAGES", metadata
                except Exception:
                    return False, "CORRUPT_PDF", metadata

            elif extension == ".docx":
                metadata["is_text"] = False
                is_valid, failure_code = self._validate_docx_structure(path)
                if not is_valid:
                    return False, failure_code, metadata

            elif extension == ".odt":
                metadata["is_text"] = False
                is_valid, failure_code = self._validate_odt_structure(path)
                if not is_valid:
                    return False, failure_code, metadata

            elif extension == ".doc":
                metadata["is_text"] = False
                if not self._read_prefix(path, len(OLE_COMPOUND_HEADER)).startswith(OLE_COMPOUND_HEADER):
                    return False, "STRUCTURAL_CORRUPTION", metadata

            elif extension == ".rtf":
                text, encoding = self._read_text_document(path)
                if text is None:
                    return False, "TEXT_DECODE_FAILED", metadata
                metadata["encoding"] = encoding
                if not text.strip():
                    return False, "FILE_EMPTY", metadata
                if not text.lstrip().startswith("{\\rtf") or "}" not in text:
                    return False, "STRUCTURAL_CORRUPTION", metadata

            elif extension in {'.txt', '.md', '.markdown', '.json', '.csv', '.tsv', '.html', '.htm'}:
                text, encoding = self._read_text_document(path)
                if text is None:
                    return False, "TEXT_DECODE_FAILED", metadata
                metadata["encoding"] = encoding
                if not text.strip():
                    return False, "FILE_EMPTY", metadata

                if extension == ".json":
                    try:
                        json.loads(text)
                    except json.JSONDecodeError:
                        return False, "JSON_INVALID", metadata

                if extension in {".csv", ".tsv"}:
                    is_valid, failure_code = self._validate_csv_structure(text, extension)
                    if not is_valid:
                        return False, failure_code, metadata

                if extension in HTML_EXTENSIONS:
                    is_valid, failure_code = self._validate_html_structure(text)
                    if not is_valid:
                        return False, failure_code, metadata
        except Exception as e:
            logger.warning(f"Structural validation failed for {path.name}: {e}")
            return False, "STRUCTURAL_CORRUPTION", metadata

        return True, "", metadata

    def _failure_values(
        self,
        status: str,
        failure_code: str,
        failure_stage: str,
        failure_message: str | None = None,
        processed: bool = False,
        **extra_values,
    ) -> dict:
        values = {
            "Status": status,
            "FailureCode": failure_code,
            "FailureMessage": failure_message or FAILURE_MESSAGES.get(failure_code, failure_code),
            "FailureStage": failure_stage,
            "LastAttemptAt": datetime.utcnow(),
        }
        if processed:
            values["ProcessedDate"] = datetime.utcnow()
        values.update(extra_values)
        return values

    def _transition_upload_status(
        self,
        db: Session,
        upload_id: uuid.UUID,
        expected_statuses: set[str],
        new_status: str,
        **extra_values,
    ) -> bool:
        values = {
            "Status": new_status,
            "LastAttemptAt": datetime.utcnow(),
        }
        values.update(extra_values)

        result = db.execute(
            update(Upload)
            .where(Upload.UploadId == upload_id)
            .where(Upload.Status.in_(expected_statuses))
            .values(**values)
        )
        return result.rowcount == 1

    def _mark_upload_failure(
        self,
        db: Session,
        upload_id: uuid.UUID,
        expected_statuses: set[str],
        status: str,
        failure_code: str,
        failure_stage: str,
        failure_message: str | None = None,
        processed: bool = True,
        **extra_values,
    ) -> bool:
        values = self._failure_values(
            status=status,
            failure_code=failure_code,
            failure_stage=failure_stage,
            failure_message=failure_message,
            processed=processed,
            **extra_values,
        )
        result = db.execute(
            update(Upload)
            .where(Upload.UploadId == upload_id)
            .where(Upload.Status.in_(expected_statuses))
            .values(**values)
        )
        return result.rowcount == 1

    def _ensure_ingestion_job(self, db: Session, binary_hash: str) -> bool:
        existing_job = db.query(FileIngestion).filter(FileIngestion.BinaryHash == binary_hash).first()
        if existing_job:
            return False

        db.add(
            FileIngestion(
                BinaryHash=binary_hash,
                Status="Pending",
                Stage="AdmissionComplete",
            )
        )
        db.flush()
        return True

    def _ensure_metadata_and_job(
        self,
        db: Session,
        upload_id: uuid.UUID,
        binary_hash: str,
        extension: str,
        detected_mime: str,
        mime_source: str,
        file_size: int,
        meta_hints: dict,
    ) -> bool:
        existing_metadata = db.query(FileMetadata).filter(FileMetadata.BinaryHash == binary_hash).first()
        if existing_metadata:
            try:
                self._ensure_ingestion_job(db, binary_hash)
            except IntegrityError:
                db.rollback()
            return False

        try:
            db.add(
                FileMetadata(
                    BinaryHash=binary_hash,
                    Extension=extension,
                    DetectedMimeType=detected_mime,
                    OriginalMimeTypeSource=mime_source,
                    FileSize=file_size,
                    IsEncrypted=meta_hints.get("is_encrypted", False),
                    IsTextBased=meta_hints.get("is_text", True),
                    PageCount=meta_hints.get("pages"),
                    FirstUploadId=upload_id,
                    CreatedByAdmissionVersion=ADMISSION_VERSION,
                )
            )
            db.flush()
            self._ensure_ingestion_job(db, binary_hash)
            return True
        except IntegrityError:
            db.rollback()
            existing_metadata = db.query(FileMetadata).filter(FileMetadata.BinaryHash == binary_hash).first()
            if existing_metadata:
                try:
                    self._ensure_ingestion_job(db, binary_hash)
                except IntegrityError:
                    db.rollback()
                return False
            raise

    def _quarantine_file_sync(
        self,
        db: Session,
        path: Path,
        reason: str,
        upload_id: uuid.UUID,
        failure_stage: str,
        failure_message: str | None = None,
        detected_mime: str | None = None,
        extension: str | None = None,
    ):
        """Moves file to quarantine and updates DB."""
        dest = self.quarantine_dir / f"{reason}_{path.name}"
        try:
            shutil.move(str(path), str(dest))
            self._mark_upload_failure(
                db,
                upload_id,
                expected_statuses={"Validating"},
                status="Rejected",
                failure_code=reason,
                failure_stage=failure_stage,
                failure_message=failure_message,
                QuarantinePath=str(dest),
                DetectedMimeType=detected_mime,
                Extension=extension,
            )
            db.commit()
            logger.info(f"File {path.name} quarantined: {reason}")
        except Exception as e:
            db.rollback()
            logger.error(f"Failed to quarantine {path.name}: {e}")
            self._mark_upload_failure(
                db,
                upload_id,
                expected_statuses={"Validating"},
                status="AdmissionError",
                failure_code="INTERNAL_ERROR",
                failure_stage="Quarantine",
                failure_message=str(e),
                processed=False,
            )
            db.commit()

    async def quarantine_file(
        self,
        db: Session,
        path: Path,
        reason: str,
        upload_id: uuid.UUID,
        failure_stage: str,
        failure_message: str | None = None,
        detected_mime: str | None = None,
        extension: str | None = None,
    ):
        await asyncio.to_thread(
            self._quarantine_file_sync,
            db,
            path,
            reason,
            upload_id,
            failure_stage,
            failure_message,
            detected_mime,
            extension,
        )

    def _is_candidate_file(self, path: Path) -> bool:
        if not path.is_file() or path.suffix == '.tmp':
            return False

        try:
            path.relative_to(self.quarantine_dir)
            return False
        except ValueError:
            return True

    async def process_file(self, file_path: str):
        path = Path(file_path).resolve()
        is_candidate = await asyncio.to_thread(self._is_candidate_file, path)
        if not is_candidate:
            return

        async with self._inflight_lock:
            if path in self._inflight_paths:
                logger.info(f"Skipping {path.name}: path already has an admission task")
                return
            self._inflight_paths.add(path)

        try:
            if WATCH_EVENT_DEBOUNCE_SECONDS > 0:
                await asyncio.sleep(WATCH_EVENT_DEBOUNCE_SECONDS)

            is_candidate = await asyncio.to_thread(self._is_candidate_file, path)
            if not is_candidate:
                return

            await asyncio.to_thread(self._process_file_sync, path)
        finally:
            async with self._inflight_lock:
                self._inflight_paths.discard(path)

    def _process_file_sync(self, path: Path):
        if not path.is_file() or path.suffix == '.tmp':
            return

        try:
            path.relative_to(self.quarantine_dir)
            return
        except ValueError:
            pass

        filename = path.name
        extension = path.suffix.lower()
        
        # 1. Extract UploadId
        try:
            parts = filename.split("__")
            if len(parts) < 2: return
            upload_id = uuid.UUID(parts[1].split(".")[0])
        except: return

        db: Session = SessionLocal()
        try:
            upload_exists = db.query(Upload).filter(Upload.UploadId == upload_id).first()
            if not upload_exists:
                logger.info(f"Deferring {filename}: upload row is not visible yet")
                return

            if not self._transition_upload_status(
                db,
                upload_id,
                expected_statuses={"Pending"},
                new_status="Stabilizing",
            ):
                db.rollback()
                current_upload = db.query(Upload).filter(Upload.UploadId == upload_id).first()
                current_status = current_upload.Status if current_upload else "missing"
                logger.info(f"Skipping {filename}: upload is already {current_status}")
                return
            db.commit()

            if not self._wait_for_stable_file_sync(path):
                self._mark_upload_failure(
                    db,
                    upload_id,
                    expected_statuses={"Stabilizing"},
                    status="Pending",
                    failure_code="FILE_NOT_STABLE",
                    failure_stage="Stabilizing",
                    processed=False,
                )
                db.commit()
                logger.info(f"Deferring {path.name}: file is not stable yet")
                return

            # 2. State Transition: Stabilizing -> Validating
            if not self._transition_upload_status(
                db,
                upload_id,
                expected_statuses={"Stabilizing"},
                new_status="Validating",
            ):
                db.rollback()
                logger.info(f"Skipping {filename}: could not transition to Validating")
                return
            db.commit()

            # 3. Content-based MIME detection
            kind = filetype.guess(path)
            sniffed_mime = kind.mime if kind else None

            # 4. Whitelist & Extension Match
            if extension not in SUPPORTED_EXTENSIONS:
                self._quarantine_file_sync(
                    db,
                    path,
                    "UNSUPPORTED_EXTENSION",
                    upload_id,
                    failure_stage="ExtensionValidation",
                    extension=extension,
                )
                return

            is_mime_valid, mime_fail_code, detected_mime, mime_source = self.validate_extension_mime_sync(
                path,
                extension,
                sniffed_mime,
            )
            if not is_mime_valid:
                self._quarantine_file_sync(
                    db,
                    path,
                    mime_fail_code,
                    upload_id,
                    failure_stage="ContentTypeValidation",
                    detected_mime=detected_mime,
                    extension=extension,
                )
                return

            # 5. Structural Validation
            is_valid, fail_code, meta_hints = self._validate_file_structure(path, extension)
            if not is_valid:
                self._quarantine_file_sync(
                    db,
                    path,
                    fail_code,
                    upload_id,
                    failure_stage="StructuralValidation",
                    detected_mime=detected_mime,
                    extension=extension,
                )
                return

            # 6. Binary Hashing
            try:
                binary_hash = self._get_binary_hash(path)
            except Exception as hash_error:
                self._mark_upload_failure(
                    db,
                    upload_id,
                    expected_statuses={"Validating"},
                    status="AdmissionError",
                    failure_code="HASH_COMPUTE_FAILED",
                    failure_stage="Hashing",
                    failure_message=str(hash_error),
                    DetectedMimeType=detected_mime,
                    Extension=extension,
                    processed=False,
                )
                db.commit()
                return

            # 7. Deduplication Logic
            file_size = path.stat().st_size
            try:
                created_metadata = self._ensure_metadata_and_job(
                    db,
                    upload_id,
                    binary_hash,
                    extension,
                    detected_mime,
                    mime_source,
                    file_size,
                    meta_hints,
                )
            except IntegrityError as db_error:
                db.rollback()
                self._mark_upload_failure(
                    db,
                    upload_id,
                    expected_statuses={"Validating"},
                    status="AdmissionError",
                    failure_code="DB_WRITE_FAILED",
                    failure_stage="DatabaseWrite",
                    failure_message=str(db_error),
                    DetectedMimeType=detected_mime,
                    Extension=extension,
                    processed=False,
                )
                db.commit()
                return

            if not created_metadata:
                logger.info(f"Binary duplicate: {filename}")
                self._mark_upload_failure(
                    db,
                    upload_id,
                    expected_statuses={"Validating"},
                    status="DuplicateBinary",
                    failure_code="DUPLICATE_BINARY",
                    failure_stage="Deduplication",
                    BinaryHash=binary_hash,
                    DetectedMimeType=detected_mime,
                    Extension=extension,
                )
            else:
                accept_result = db.execute(
                    update(Upload)
                    .where(Upload.UploadId == upload_id)
                    .where(Upload.Status == "Validating")
                    .values(Status="Accepted", BinaryHash=binary_hash, ProcessedDate=datetime.utcnow())
                    .values(
                        DetectedMimeType=detected_mime,
                        Extension=extension,
                        FailureCode=None,
                        FailureMessage=None,
                        FailureStage=None,
                    )
                )
                if accept_result.rowcount != 1:
                    db.rollback()
                    logger.info(f"Skipping {filename}: upload left Validating before accept")
                    return
            
            db.commit()
            logger.info(f"Successfully admitted {filename}")

        except Exception as e:
            logger.exception(f"Error admitting {filename}: {e}")
            db.rollback()
            self._mark_upload_failure(
                db,
                upload_id,
                expected_statuses={"Pending", "Stabilizing", "Validating"},
                status="AdmissionError",
                failure_code="INTERNAL_ERROR",
                failure_stage="Admission",
                failure_message=str(e),
                processed=False,
            )
            db.commit()
        finally:
            db.close()

    def _recover_stuck_uploads_sync(self):
        """Startup recovery for files stuck in intermediate states."""
        db = SessionLocal()
        try:
            stuck = db.query(Upload).filter(Upload.Status.in_(["Validating", "Stabilizing"])).all()
            if stuck:
                logger.info(f"Recovering {len(stuck)} stuck uploads...")
                for up in stuck:
                    up.Status = "Pending"
                    up.RetryCount += 1
                db.commit()
        except Exception as e:
            logger.error(f"Recovery failed: {e}")
        finally:
            db.close()

    async def recover_stuck_uploads(self):
        await asyncio.to_thread(self._recover_stuck_uploads_sync)

    def _should_process_change(self, change_type: Change) -> bool:
        return change_type in {Change.added, Change.modified}

    def _track_processing_task(self, task: asyncio.Task):
        self._processing_tasks.add(task)
        task.add_done_callback(self._processing_tasks.discard)

    async def handle_watch_changes(self, changes):
        for change_type, file_path in changes:
            if self._should_process_change(change_type):
                task = asyncio.create_task(self.process_file(file_path))
                self._track_processing_task(task)

    async def wait_for_processing_tasks(self):
        if not self._processing_tasks:
            return

        tasks = list(self._processing_tasks)
        await asyncio.gather(*tasks, return_exceptions=True)

    async def start(self):
        await self.recover_stuck_uploads()
        self.is_running = True
        logger.info(f"Harden Worker active. Watching: {self.watch_dir}")

        while self.is_running:
            try:
                async for changes in awatch(self.watch_dir):
                    await self.handle_watch_changes(changes)
            except Exception as e:
                logger.error(f"Watcher loop error: {e}. Restarting in 5s...")
                await asyncio.sleep(5)

    def stop(self):
        self.is_running = False
        for task in list(self._processing_tasks):
            task.cancel()

if __name__ == "__main__":
    watcher = FileWatcher(WATCH_DIR)
    try:
        asyncio.run(watcher.start())
    except KeyboardInterrupt:
        watcher.stop()

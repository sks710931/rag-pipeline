import asyncio
import os
import logging
import hashlib
import mimetypes
import uuid
import filetype
import shutil
from pathlib import Path
from datetime import datetime
from dotenv import load_dotenv
from watchfiles import awatch, Change
from sqlalchemy.orm import Session
from sqlalchemy import update
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
ADMISSION_VERSION = "1.0"
STABILITY_CHECKS = int(os.getenv("FILE_STABILITY_CHECKS", "3"))
STABILITY_INTERVAL_SECONDS = float(os.getenv("FILE_STABILITY_INTERVAL_SECONDS", "1.0"))

SUPPORTED_EXTENSIONS = {
    '.txt', '.pdf', '.doc', '.docx', '.md', '.markdown', 
    '.html', '.htm', '.rtf', '.odt', '.csv', '.tsv', '.json'
}

class FileWatcher:
    def __init__(self, watch_dir: Path):
        self.watch_dir = Path(watch_dir).resolve()
        self.quarantine_dir = self.watch_dir / "quarantine"
        self.is_running = False
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

    def _validate_file_structure(self, path: Path, extension: str) -> tuple[bool, str, dict]:
        """Lightweight structural validation."""
        if path.stat().st_size == 0:
            return False, "FILE_EMPTY", {}

        if path.stat().st_size > MAX_FILE_SIZE:
            return False, "FILE_TOO_LARGE", {}

        metadata = {"pages": None, "is_encrypted": False, "is_text": True}

        try:
            if extension == '.pdf':
                reader = PdfReader(path)
                metadata["pages"] = len(reader.pages)
                metadata["is_encrypted"] = reader.is_encrypted
                metadata["is_text"] = False
                if reader.is_encrypted:
                    return False, "ENCRYPTED_PDF", metadata

            # Simple text decodability check for text-like formats
            if extension in {'.txt', '.md', '.markdown', '.json', '.csv', '.tsv', '.html', '.htm'}:
                with open(path, 'r', encoding='utf-8') as f:
                    f.read(1024) # Try reading first 1KB
        except Exception as e:
            logger.warning(f"Structural validation failed for {path.name}: {e}")
            return False, "STRUCTURAL_CORRUPTION", metadata

        return True, "", metadata

    async def quarantine_file(self, path: Path, reason: str, upload_id: uuid.UUID):
        """Moves file to quarantine and updates DB."""
        dest = self.quarantine_dir / f"{reason}_{path.name}"
        try:
            shutil.move(str(path), str(dest))
            db = SessionLocal()
            db.execute(
                update(Upload)
                .where(Upload.UploadId == upload_id)
                .values(
                    Status="Rejected",
                    FailureCode=reason,
                    QuarantinePath=str(dest),
                    ProcessedDate=datetime.utcnow()
                )
            )
            db.commit()
            db.close()
            logger.info(f"File {path.name} quarantined: {reason}")
        except Exception as e:
            logger.error(f"Failed to quarantine {path.name}: {e}")

    async def process_file(self, file_path: str):
        path = Path(file_path)
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
            upload = db.query(Upload).filter(Upload.UploadId == upload_id).first()
            if not upload:
                logger.info(f"Deferring {filename}: upload row is not visible yet")
                return

            if upload.Status not in {"Pending", "Stabilizing"}:
                logger.info(f"Skipping {filename}: upload is already {upload.Status}")
                return

            upload.Status = "Stabilizing"
            upload.LastAttemptAt = datetime.utcnow()
            db.commit()

            if not await self.wait_for_stable_file(path):
                upload.Status = "Pending"
                upload.LastAttemptAt = datetime.utcnow()
                db.commit()
                logger.info(f"Deferring {path.name}: file is not stable yet")
                return

            # 2. State Transition: Stabilizing -> Validating
            upload.Status = "Validating"
            upload.LastAttemptAt = datetime.utcnow()
            db.commit()

            # 3. Content-based MIME detection
            kind = await asyncio.to_thread(filetype.guess, path)
            sniffed_mime = kind.mime if kind else None

            # 4. Whitelist & Extension Match
            if extension not in SUPPORTED_EXTENSIONS:
                await self.quarantine_file(path, "UNSUPPORTED_EXTENSION", upload_id)
                return

            # 5. Structural Validation
            is_valid, fail_code, meta_hints = await asyncio.to_thread(self._validate_file_structure, path, extension)
            if not is_valid:
                await self.quarantine_file(path, fail_code, upload_id)
                return

            # 6. Binary Hashing (Non-blocking)
            binary_hash = await asyncio.to_thread(self._get_binary_hash, path)

            # 7. Deduplication Logic
            existing_metadata = db.query(FileMetadata).filter(FileMetadata.BinaryHash == binary_hash).first()

            if existing_metadata:
                logger.info(f"Binary duplicate: {filename}")
                db.execute(
                    update(Upload)
                    .where(Upload.UploadId == upload_id)
                    .values(Status="DuplicateBinary", BinaryHash=binary_hash, ProcessedDate=datetime.utcnow())
                )
            else:
                # 8. Create Canonical Metadata
                new_metadata = FileMetadata(
                    BinaryHash=binary_hash,
                    Extension=extension,
                    DetectedMimeType=sniffed_mime or mimetypes.guess_type(path)[0] or "application/octet-stream",
                    OriginalMimeTypeSource="sniffed" if sniffed_mime else "extension",
                    FileSize=path.stat().st_size,
                    IsEncrypted=meta_hints.get("is_encrypted", False),
                    IsTextBased=meta_hints.get("is_text", True),
                    PageCount=meta_hints.get("pages"),
                    FirstUploadId=upload_id,
                    CreatedByAdmissionVersion=ADMISSION_VERSION
                )
                db.add(new_metadata)
                db.flush()

                # 9. Enqueue Preprocessing
                new_ingestion = FileIngestion(
                    BinaryHash=binary_hash,
                    Status="Pending",
                    Stage="AdmissionComplete"
                )
                db.add(new_ingestion)
                
                db.execute(
                    update(Upload)
                    .where(Upload.UploadId == upload_id)
                    .values(Status="Accepted", BinaryHash=binary_hash, ProcessedDate=datetime.utcnow())
                )
            
            db.commit()
            logger.info(f"Successfully admitted {filename}")

        except Exception as e:
            logger.exception(f"Error admitting {filename}: {e}")
            db.rollback()
            db.execute(
                update(Upload)
                .where(Upload.UploadId == upload_id)
                .values(Status="AdmissionError", FailureCode="INTERNAL_ERROR", FailureMessage=str(e))
            )
            db.commit()
        finally:
            db.close()

    async def recover_stuck_uploads(self):
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

    async def start(self):
        await self.recover_stuck_uploads()
        self.is_running = True
        logger.info(f"Harden Worker active. Watching: {self.watch_dir}")

        while self.is_running:
            try:
                async for changes in awatch(self.watch_dir):
                    for change_type, file_path in changes:
                        # React to added or renamed files (atomic move)
                        if change_type in {Change.added, Change.modified}:
                            await self.process_file(file_path)
            except Exception as e:
                logger.error(f"Watcher loop error: {e}. Restarting in 5s...")
                await asyncio.sleep(5)

    def stop(self):
        self.is_running = False

if __name__ == "__main__":
    watcher = FileWatcher(WATCH_DIR)
    try:
        asyncio.run(watcher.start())
    except KeyboardInterrupt:
        watcher.stop()

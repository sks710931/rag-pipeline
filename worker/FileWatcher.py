import asyncio
import os
import logging
import hashlib
import mimetypes
import uuid
from pathlib import Path
from datetime import datetime
from dotenv import load_dotenv
from watchfiles import awatch, Change
from sqlalchemy.orm import Session
from sqlalchemy import update

# Local imports
from database import SessionLocal
from models import Upload, FileMetadata, FileIngestion

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger("Worker")

# Whitelist of document-based file extensions
SUPPORTED_EXTENSIONS = {
    '.txt', '.pdf', '.doc', '.docx', '.md', '.markdown', 
    '.html', '.htm', '.rtf', '.odt', '.csv', '.tsv', '.json'
}

class FileWatcher:
    def __init__(self, watch_dir: str):
        self.watch_dir = Path(watch_dir).resolve()
        self.is_running = False

    async def get_file_hash(self, file_path: Path) -> str:
        sha256_hash = hashlib.sha256()
        with open(file_path, "rb") as f:
            for byte_block in iter(lambda: f.read(4096), b""):
                sha256_hash.update(byte_block)
        return sha256_hash.hexdigest()

    async def process_file(self, file_path: str):
        path = Path(file_path)
        if not path.is_file():
            return

        filename = path.name
        extension = path.suffix.lower()
        
        # 1. Extract UploadId from filename
        try:
            parts = filename.split("__")
            if len(parts) < 2:
                logger.warning(f"File {filename} does not follow naming convention, skipping.")
                return
            guid_str = parts[1].split(".")[0]
            upload_id = uuid.UUID(guid_str)
        except Exception as e:
            logger.error(f"Could not parse UploadId from {filename}: {e}")
            return

        db: Session = SessionLocal()
        try:
            # 2. Check if file format is supported
            if extension not in SUPPORTED_EXTENSIONS:
                logger.warning(f"REJECTED: Unsupported file format {extension} for {filename}. Deleting...")
                db.execute(
                    update(Upload)
                    .where(Upload.UploadId == upload_id)
                    .values(Status="Rejected", ProcessedDate=datetime.utcnow())
                )
                db.commit()
                if path.exists():
                    os.remove(path)
                return

            # 3. Update status to Processing
            db.execute(
                update(Upload)
                .where(Upload.UploadId == upload_id)
                .values(Status="Processing")
            )
            db.commit()

            # 4. Metadata extraction
            file_hash = await self.get_file_hash(path)
            file_size = path.stat().st_size
            mime_type, _ = mimetypes.guess_type(path)
            mime_type = mime_type or "application/octet-stream"

            # 5. Deduplication logic
            existing_metadata = db.query(FileMetadata).filter(FileMetadata.ContentHash == file_hash).first()

            if existing_metadata:
                logger.info(f"Duplicate content detected for {filename}. Hash: {file_hash}")
                db.execute(
                    update(Upload)
                    .where(Upload.UploadId == upload_id)
                    .values(Status="Duplicate", ProcessedDate=datetime.utcnow())
                )
            else:
                logger.info(f"New unique content found: {filename}")
                new_metadata = FileMetadata(
                    ContentHash=file_hash,
                    MimeType=mime_type,
                    FileSize=file_size,
                    FirstUploadId=upload_id
                )
                db.add(new_metadata)
                db.flush() # Ensure metadata exists before ingestion refers to it
                
                # Create a pending ingestion record
                new_ingestion = FileIngestion(
                    ContentHash=file_hash,
                    Status="Pending"
                )
                db.add(new_ingestion)
                
                db.execute(
                    update(Upload)
                    .where(Upload.UploadId == upload_id)
                    .values(Status="Processed", ProcessedDate=datetime.utcnow())
                )
            
            db.commit()
            logger.info(f"Successfully processed {filename}")

        except Exception as e:
            logger.exception(f"Error during processing {filename}: {e}")
            db.rollback()
            try:
                db.execute(
                    update(Upload)
                    .where(Upload.UploadId == upload_id)
                    .values(Status="Error")
                )
                db.commit()
            except:
                pass
        finally:
            db.close()

    async def start(self):
        if not self.watch_dir.exists():
            self.watch_dir.mkdir(parents=True, exist_ok=True)

        self.is_running = True
        logger.info(f"Worker service active. Watching: {self.watch_dir}")

        while self.is_running:
            try:
                async for changes in awatch(self.watch_dir):
                    for change_type, file_path in changes:
                        if change_type == Change.added:
                            await asyncio.sleep(0.5)
                            await self.process_file(file_path)
            except Exception as e:
                logger.error(f"Watcher error: {e}. Restarting in 5s...")
                await asyncio.sleep(5)

    def stop(self):
        self.is_running = False

if __name__ == "__main__":
    load_dotenv()
    watch_path = os.getenv("WATCH_DIR", "./uploads")
    watcher = FileWatcher(watch_path)
    
    try:
        asyncio.run(watcher.start())
    except KeyboardInterrupt:
        watcher.stop()

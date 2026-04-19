import aiofiles
import os
import uuid
from pathlib import Path
from fastapi import UploadFile
from sqlalchemy.orm import Session
from backend.src.core.config import settings
from backend.src.core.base import BaseService, handle_errors
from backend.src.models.upload import Upload

class FileService(BaseService):
    def __init__(self):
        super().__init__()
        self.upload_dir = settings.WATCH_DIR
        self._ensure_dir()

    def _ensure_dir(self):
        if not self.upload_dir.exists():
            self.upload_dir.mkdir(parents=True, exist_ok=True)
            self.logger.info(f"Created upload directory: {self.upload_dir}")

    @handle_errors
    async def save_upload(self, file: UploadFile, db: Session, user_email: str) -> Upload:
        # 1. Generate GUID for unique storage
        upload_id = uuid.uuid4()
        original_path = Path(file.filename)
        filename_without_ext = original_path.stem
        extension = original_path.suffix
        
        # 2. Construct names: filename__guid.ext and filename__guid.tmp
        final_filename = f"{filename_without_ext}__{upload_id}{extension}"
        temp_filename = f"{final_filename}.tmp"
        
        final_path = self.upload_dir / final_filename
        temp_path = self.upload_dir / temp_filename

        # 3. Save file to .tmp location first. The watcher ignores .tmp files.
        async with aiofiles.open(temp_path, "wb") as out_file:
            content = await file.read()
            await out_file.write(content)

        # 4. Create and commit the upload row before finalizing the file name.
        # This makes the atomic rename the handoff signal: once the watcher can
        # see the final file, it can also see the corresponding DB row.
        db_upload = Upload(
            UploadId=upload_id,
            OriginalFileName=original_path.name,
            StoredFileName=final_filename,
            FilePath=str(final_path),
            UploadedBy=user_email,
            Status="Pending",
            Extension=extension.lower(),
            AdmissionVersion="1.0"
        )
        
        db.add(db_upload)
        db.commit()

        try:
            # 5. Atomic rename to final extension
            os.rename(temp_path, final_path)
        except Exception as exc:
            db_upload.Status = "AdmissionError"
            db_upload.FailureCode = "FINALIZE_RENAME_FAILED"
            db_upload.FailureMessage = str(exc)
            db.commit()
            raise
        
        db.refresh(db_upload)
        self.logger.info(f"Successfully saved file {original_path.name} as {final_filename}")
        return db_upload

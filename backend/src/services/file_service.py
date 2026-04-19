import aiofiles
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
        
        # 2. Construct storage name: filename__{guid}.ext
        stored_filename = f"{filename_without_ext}__{upload_id}{extension}"
        file_path = self.upload_dir / stored_filename
        
        # 3. Save file to disk
        async with aiofiles.open(file_path, "wb") as out_file:
            content = await file.read()
            await out_file.write(content)
        
        # 4. Create database record
        db_upload = Upload(
            UploadId=upload_id,
            OriginalFileName=original_path.name,
            StoredFileName=stored_filename,
            FilePath=str(file_path),
            UploadedBy=user_email,
            Status="Pending"
        )
        
        db.add(db_upload)
        db.commit()
        db.refresh(db_upload)
        
        self.logger.info(f"Successfully saved file {original_path.name} as {stored_filename}")
        return db_upload

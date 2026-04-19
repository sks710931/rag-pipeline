from fastapi import UploadFile, Depends
from sqlalchemy.orm import Session
from backend.src.services.file_service import FileService
from backend.src.core.base import handle_errors
from backend.src.core.database import get_db

class UploadController:
    def __init__(self):
        self.file_service = FileService()

    @handle_errors
    async def upload_file(self, file: UploadFile, db: Session, user_claims: dict):
        user_email = user_claims.get("email", "unknown")
        
        db_upload = await self.file_service.save_upload(
            file=file, 
            db=db, 
            user_email=user_email
        )
        
        return {
            "upload_id": str(db_upload.UploadId),
            "filename": db_upload.OriginalFileName,
            "status": db_upload.Status
        }

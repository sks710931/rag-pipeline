from sqlalchemy.orm import Session
from sqlalchemy import desc
from backend.src.models.file_ingestion import FileIngestion
from backend.src.models.file_metadata import FileMetadata
from backend.src.models.upload import Upload
from backend.src.core.base import handle_errors

class IngestionController:
    @handle_errors
    async def get_ingestions(self, db: Session, status: str = None):
        query = db.query(FileIngestion, FileMetadata, Upload)\
            .join(FileMetadata, FileIngestion.ContentHash == FileMetadata.ContentHash)\
            .join(Upload, FileMetadata.FirstUploadId == Upload.UploadId)
        
        if status:
            query = query.filter(FileIngestion.Status == status)
            
        results = query.order_by(desc(FileIngestion.CreatedAt)).all()
        
        return [
            {
                "ingestion_id": str(ing.IngestionId),
                "filename": up.OriginalFileName,
                "status": ing.Status,
                "mime_type": meta.MimeType,
                "size": meta.FileSize,
                "created_at": ing.CreatedAt,
                "error": ing.ErrorMessage
            }
            for ing, meta, up in results
        ]

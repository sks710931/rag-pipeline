from sqlalchemy import Column, String, DateTime, Index
from sqlalchemy.dialects.mssql import UNIQUEIDENTIFIER
from sqlalchemy.sql import func
from backend.src.core.database import Base
import uuid

class Upload(Base):
    __tablename__ = "uploads"

    UploadId = Column(UNIQUEIDENTIFIER, primary_key=True, default=uuid.uuid4)
    OriginalFileName = Column(String(512), nullable=False)
    StoredFileName = Column(String(1024), nullable=False)
    FilePath = Column(String, nullable=False)
    Status = Column(String(50), default="Pending")
    UploadedBy = Column(String(256))
    UploadDate = Column(DateTime, server_default=func.now())
    ProcessedDate = Column(DateTime)

    __table_args__ = (
        Index('IX_Uploads_Status', 'Status'),
    )

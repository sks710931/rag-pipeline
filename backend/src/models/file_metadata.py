from sqlalchemy import Column, String, BigInteger, DateTime, ForeignKey, CHAR
from sqlalchemy.dialects.mssql import UNIQUEIDENTIFIER
from sqlalchemy.sql import func
from backend.src.core.database import Base

class FileMetadata(Base):
    __tablename__ = "file_metadata"

    ContentHash = Column(CHAR(64), primary_key=True)
    MimeType = Column(String(100), nullable=False)
    FileSize = Column(BigInteger, nullable=False)
    FirstUploadId = Column(UNIQUEIDENTIFIER, ForeignKey("uploads.UploadId"), nullable=False)
    CreatedAt = Column(DateTime, server_default=func.now())

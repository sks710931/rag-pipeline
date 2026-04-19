from sqlalchemy import Column, String, BigInteger, DateTime, ForeignKey, CHAR, Index, Text
from sqlalchemy.dialects.mssql import UNIQUEIDENTIFIER
from sqlalchemy.sql import func
import uuid
from database import Base

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

class FileMetadata(Base):
    __tablename__ = "file_metadata"

    ContentHash = Column(CHAR(64), primary_key=True)
    MimeType = Column(String(100), nullable=False)
    FileSize = Column(BigInteger, nullable=False)
    FirstUploadId = Column(UNIQUEIDENTIFIER, ForeignKey("uploads.UploadId"), nullable=False)
    CreatedAt = Column(DateTime, server_default=func.now())

class FileIngestion(Base):
    __tablename__ = "file_ingestions"

    IngestionId = Column(UNIQUEIDENTIFIER, primary_key=True, default=uuid.uuid4)
    ContentHash = Column(CHAR(64), ForeignKey("file_metadata.ContentHash"), nullable=False)
    Status = Column(String(50), default="Pending")
    ErrorMessage = Column(Text)
    StartTime = Column(DateTime)
    EndTime = Column(DateTime)
    CreatedAt = Column(DateTime, server_default=func.now())

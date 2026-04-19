from sqlalchemy import Column, String, BigInteger, DateTime, ForeignKey, CHAR, Boolean, Integer
from sqlalchemy.dialects.mssql import UNIQUEIDENTIFIER
from sqlalchemy.sql import func
from backend.src.core.database import Base

class FileMetadata(Base):
    __tablename__ = "file_metadata"

    BinaryHash = Column(CHAR(64), primary_key=True)
    Extension = Column(String(20), nullable=False)
    DetectedMimeType = Column(String(100), nullable=False)
    OriginalMimeTypeSource = Column(String(50), nullable=False) # extension, sniffed, parser
    FileSize = Column(BigInteger, nullable=False)
    IsEncrypted = Column(Boolean, default=False)
    IsTextBased = Column(Boolean)
    PageCount = Column(Integer)
    ContentHashNormalized = Column(CHAR(64))
    ParserHint = Column(String(50))
    FirstUploadId = Column(UNIQUEIDENTIFIER, ForeignKey("uploads.UploadId"), nullable=False)
    CreatedAt = Column(DateTime, server_default=func.now())
    CreatedByAdmissionVersion = Column(String(20), default="1.0")

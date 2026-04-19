from sqlalchemy import Column, String, BigInteger, DateTime, ForeignKey, CHAR, Index, Text, Integer, Boolean, UniqueConstraint
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
    
    # Admission Details
    BinaryHash = Column(String(64))
    DetectedMimeType = Column(String(100))
    Extension = Column(String(20))
    QuarantinePath = Column(String)
    
    # Error Tracking
    FailureCode = Column(String(50))
    FailureMessage = Column(String)
    FailureStage = Column(String(50))
    
    # Audit & Retry
    UploadedBy = Column(String(256))
    UploadDate = Column(DateTime, server_default=func.now())
    ProcessedDate = Column(DateTime)
    RetryCount = Column(Integer, default=0)
    LastAttemptAt = Column(DateTime)
    AdmissionVersion = Column(String(20), default="1.0")

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

class FileIngestion(Base):
    __tablename__ = "file_ingestions"

    IngestionId = Column(UNIQUEIDENTIFIER, primary_key=True, default=uuid.uuid4)
    BinaryHash = Column(CHAR(64), ForeignKey("file_metadata.BinaryHash"), nullable=False)
    Status = Column(String(50), default="Pending")
    Stage = Column(String(50))
    ErrorMessage = Column(Text)
    
    # Worker Info
    WorkerId = Column(String(100))
    AttemptCount = Column(Integer, default=0)
    
    # Logic Versions
    PreprocessingVersion = Column(String(20))
    ChunkingVersion = Column(String(20))
    EmbeddingModel = Column(String(100))
    EmbeddingVersion = Column(String(20))
    
    # Timestamps
    StartTime = Column(DateTime)
    EndTime = Column(DateTime)
    CreatedAt = Column(DateTime, server_default=func.now())

    __table_args__ = (
        UniqueConstraint("BinaryHash", name="UQ_FileIngestions_BinaryHash"),
        Index("IX_FileIngestions_Status", "Status"),
        Index("IX_FileIngestions_Stage", "Stage"),
    )

from sqlalchemy import Column, String, DateTime, Index, Integer
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

    __table_args__ = (
        Index('IX_Uploads_Status', 'Status'),
        Index('IX_Uploads_BinaryHash', 'BinaryHash'),
    )

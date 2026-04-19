from sqlalchemy import Column, String, DateTime, ForeignKey, CHAR, Text, Integer
from sqlalchemy.dialects.mssql import UNIQUEIDENTIFIER
from sqlalchemy.sql import func
from backend.src.core.database import Base
import uuid

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

from sqlalchemy import Column, String, DateTime, ForeignKey, CHAR, Text
from sqlalchemy.dialects.mssql import UNIQUEIDENTIFIER
from sqlalchemy.sql import func
from backend.src.core.database import Base
import uuid

class FileIngestion(Base):
    __tablename__ = "file_ingestions"

    IngestionId = Column(UNIQUEIDENTIFIER, primary_key=True, default=uuid.uuid4)
    ContentHash = Column(CHAR(64), ForeignKey("file_metadata.ContentHash"), nullable=False)
    Status = Column(String(50), default="Pending")
    ErrorMessage = Column(Text)
    StartTime = Column(DateTime)
    EndTime = Column(DateTime)
    CreatedAt = Column(DateTime, server_default=func.now())

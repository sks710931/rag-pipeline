import os
from pydantic_settings import BaseSettings
from pathlib import Path
from dotenv import load_dotenv

load_dotenv()

class Settings(BaseSettings):
    PROJECT_NAME: str = os.getenv("PROJECT_NAME", "RAG Pipeline API")
    VERSION: str = os.getenv("VERSION", "1.0.0")
    API_V1_STR: str = "/api"
    
    WATCH_DIR: Path = Path(os.getenv("WATCH_DIR", "./uploads"))
    LOG_LEVEL: str = os.getenv("LOG_LEVEL", "INFO")
    
    # OAuth2 Configuration
    OIDC_AUTHORITY: str = os.getenv("OIDC_AUTHORITY", "")
    OIDC_AUDIENCE: str = os.getenv("OIDC_AUDIENCE", "")
    
    # Database Configuration
    DB_SERVER: str = os.getenv("DB_SERVER", "localhost")
    DB_NAME: str = os.getenv("DB_NAME", "rag-pipeline")
    DB_DRIVER: str = os.getenv("DB_DRIVER", "ODBC Driver 17 for SQL Server")
    DATABASE_URL: str = f"mssql+pyodbc://@{DB_SERVER}/{DB_NAME}?driver={DB_DRIVER.replace(' ', '+')}&trusted_connection=yes"
    
    # CORS
    BACKEND_CORS_ORIGINS: list = ["*"]

settings = Settings()

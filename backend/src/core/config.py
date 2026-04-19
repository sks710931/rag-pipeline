import os
from pydantic_settings import BaseSettings
from pathlib import Path
from dotenv import load_dotenv

load_dotenv()

class Settings(BaseSettings):
    PROJECT_NAME: str = "RAG Pipeline API"
    VERSION: str = "1.0.0"
    API_V1_STR: str = "/api"
    
    WATCH_DIR: Path = Path(os.getenv("WATCH_DIR", "./uploads"))
    LOG_LEVEL: str = os.getenv("LOG_LEVEL", "INFO")
    
    # OAuth2 Configuration
    OIDC_AUTHORITY: str = "https://authserver-cwa3c8ddgydva8e9.eastus-01.azurewebsites.net/"
    OIDC_AUDIENCE: str = "api://rag-pipeline" 
    
    # Database Configuration (Local SQL Server with Windows Auth)
    DB_SERVER: str = os.getenv("DB_SERVER", "localhost")
    DB_NAME: str = os.getenv("DB_NAME", "rag-pipeline")
    DATABASE_URL: str = f"mssql+pyodbc://@{DB_SERVER}/{DB_NAME}?driver=ODBC+Driver+17+for+SQL+Server&trusted_connection=yes"
    
    # CORS
    BACKEND_CORS_ORIGINS: list = ["*"]

settings = Settings()

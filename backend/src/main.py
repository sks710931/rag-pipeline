import sys
from pathlib import Path
import os

# Add the project root to sys.path to allow absolute imports
root_path = Path(__file__).resolve().parent.parent.parent
sys.path.append(str(root_path))

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from backend.src.core.config import settings
from backend.src.api.routes import upload_routes, ingestion_routes

def create_app() -> FastAPI:
    app = FastAPI(
        title=settings.PROJECT_NAME,
        version=settings.VERSION,
    )

    # Set up CORS
    app.add_middleware(
        CORSMiddleware,
        allow_origins=settings.BACKEND_CORS_ORIGINS,
        allow_credentials=True,
        allow_methods=["*"],
        allow_headers=["*"],
    )

    # Register Routers
    app.include_router(upload_routes.router, prefix=settings.API_V1_STR)
    app.include_router(ingestion_routes.router, prefix=settings.API_V1_STR)

    @app.get("/health")
    async def health_check():
        return {"status": "healthy", "version": settings.VERSION}

    return app

app = create_app()

if __name__ == "__main__":
    import uvicorn
    uvicorn.run("backend.src.main:app", host="0.0.0.0", port=8000, reload=True)

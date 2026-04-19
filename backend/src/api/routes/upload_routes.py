from fastapi import APIRouter, File, UploadFile, Depends
from sqlalchemy.orm import Session
from backend.src.controllers.upload_controller import UploadController
from backend.src.core.security import get_current_user
from backend.src.core.database import get_db

router = APIRouter(prefix="/upload", tags=["upload"])
upload_controller = UploadController()

@router.post("")
async def upload_document(
    file: UploadFile = File(...), 
    db: Session = Depends(get_db),
    user_claims: dict = Depends(get_current_user)
):
    """
    Secured endpoint for resilient file uploading with DB tracking.
    """
    return await upload_controller.upload_file(file, db, user_claims)

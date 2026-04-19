from fastapi import APIRouter, Depends, Query
from sqlalchemy.orm import Session
from typing import List, Optional
from backend.src.controllers.ingestion_controller import IngestionController
from backend.src.core.database import get_db
from backend.src.core.security import get_current_user

router = APIRouter(prefix="/ingestions", tags=["ingestions"])
controller = IngestionController()

@router.get("", dependencies=[Depends(get_current_user)])
async def list_ingestions(
    status: Optional[str] = Query(None),
    db: Session = Depends(get_db)
):
    """
    List file ingestions with optional status filtering.
    """
    return await controller.get_ingestions(db, status)

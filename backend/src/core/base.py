import logging
from typing import Any, Callable
from functools import wraps
from fastapi import HTTPException

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("Backend")

class BaseService:
    """
    Base class for all services providing shared logging and resilience utilities.
    """
    def __init__(self):
        self.logger = logging.getLogger(self.__class__.__name__)

def handle_errors(func: Callable):
    """
    Decorator for resilient error handling across services.
    """
    @wraps(func)
    async def wrapper(*args, **kwargs):
        try:
            return await func(*args, **kwargs)
        except FileNotFoundError as e:
            logger.error(f"File not found: {e}")
            raise HTTPException(status_code=404, detail="Requested file not found")
        except Exception as e:
            logger.exception(f"Unexpected error in {func.__name__}: {e}")
            raise HTTPException(status_code=500, detail="Internal server error occurred")
    return wrapper

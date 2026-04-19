from fastapi import Depends
from fastapi.security import OAuth2AuthorizationCodeBearer
from backend.src.services.auth_service import auth_service
from backend.src.core.config import settings

# This tells FastAPI where to look for the token
oauth2_scheme = OAuth2AuthorizationCodeBearer(
    authorizationUrl=f"{settings.OIDC_AUTHORITY}connect/authorize",
    tokenUrl=f"{settings.OIDC_AUTHORITY}connect/token"
)

async def get_current_user(token: str = Depends(oauth2_scheme)):
    """
    FastAPI dependency that validates the Bearer token and returns the user claims.
    """
    return await auth_service.validate_token(token)

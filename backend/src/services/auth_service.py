import httpx
from jose import jwt, JWTError
from fastapi import HTTPException, status
from backend.src.core.config import settings
from backend.src.core.base import BaseService
from typing import Optional, Dict

class AuthService(BaseService):
    def __init__(self):
        super().__init__()
        self.authority = settings.OIDC_AUTHORITY
        self.audience = settings.OIDC_AUDIENCE
        self.jwks: Optional[Dict] = None

    async def _get_jwks(self):
        """
        Fetches and caches the JSON Web Key Set from the authority.
        """
        if not self.jwks:
            try:
                # Standard OIDC discovery endpoint
                well_known_url = f"{self.authority.rstrip('/')}/.well-known/openid-configuration"
                async with httpx.AsyncClient() as client:
                    config_response = await client.get(well_known_url)
                    config_response.raise_for_status()
                    jwks_uri = config_response.json().get("jwks_uri")
                    
                    jwks_response = await client.get(jwks_uri)
                    jwks_response.raise_for_status()
                    self.jwks = jwks_response.json()
            except Exception as e:
                self.logger.error(f"Failed to fetch JWKS from authority: {e}")
                raise HTTPException(
                    status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
                    detail="Authentication server unavailable"
                )
        return self.jwks

    async def validate_token(self, token: str) -> Dict:
        """
        Validates the JWT token against the OIDC authority.
        """
        jwks = await self._get_jwks()
        
        try:
            # Note: In production, you'd want to handle multiple keys and key rotation
            # For simplicity, we decode letting jose handle key selection from JWKS
            payload = jwt.decode(
                token,
                jwks,
                algorithms=["RS256"],
                audience=self.audience,
                issuer=self.authority
            )
            return payload
        except JWTError as e:
            self.logger.warning(f"Token validation failed: {e}")
            raise HTTPException(
                status_code=status.HTTP_401_UNAUTHORIZED,
                detail="Could not validate credentials",
                headers={"WWW-Authenticate": "Bearer"},
            )
        except Exception as e:
            self.logger.exception(f"Unexpected error during token validation: {e}")
            raise HTTPException(
                status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
                detail="Internal authentication error"
            )

# Singleton instance
auth_service = AuthService()

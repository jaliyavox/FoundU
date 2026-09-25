"""Fail-closed authentication for trusted calls from the FoundU API service."""

import os
import secrets
from typing import Annotated

from fastapi import Header, HTTPException, status

SERVICE_KEY_ENVIRONMENT_VARIABLE = "AI_SERVICE_KEY"
SERVICE_KEY_HEADER = "X-FoundU-Service-Key"
MINIMUM_SERVICE_KEY_LENGTH = 32


def _configured_service_key() -> str | None:
    """Return a usable server-side key without ever exposing its value."""
    key = os.getenv(SERVICE_KEY_ENVIRONMENT_VARIABLE)
    if (
        key is None
        or not key.strip()
        or len(key) < MINIMUM_SERVICE_KEY_LENGTH
        or any(
            character.isascii() and (ord(character) < 32 or ord(character) == 127)
            for character in key
        )
        or "replace" in key.lower()
        or "placeholder" in key.lower()
    ):
        return None
    return key


def require_service_auth(
    supplied_key: Annotated[str | None, Header(alias=SERVICE_KEY_HEADER)] = None,
) -> None:
    """Require the configured ASP.NET-to-FastAPI service credential.

    This boundary deliberately reads only server configuration and the HTTP header;
    request bodies, agent payloads, and model-generated values cannot authenticate.
    """
    configured_key = _configured_service_key()
    if configured_key is None:
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="Service authentication is unavailable.",
        )

    if supplied_key is None or not supplied_key.strip() or not secrets.compare_digest(
        supplied_key, configured_key
    ):
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Unauthorized service request.",
        )

"""Environment-backed configuration for the shared LLM boundary.

The fake and Ollama providers share this contract. Provider adapters remain behind the common
factory so agents never need provider-specific configuration.
"""

import os
from collections.abc import Mapping
from urllib.parse import urlsplit

from pydantic import BaseModel, ConfigDict, Field, ValidationError, field_validator

from app.llm.errors import LlmConfigurationError


class LlmSettings(BaseModel):
    """Non-secret settings used to compose the current LLM client."""

    model_config = ConfigDict(extra="forbid")

    provider: str = "fake"
    model: str = "fake-structured-v1"
    timeout_seconds: int = Field(default=5, ge=1, le=120)
    ollama_base_url: str | None = None

    @field_validator("provider")
    @classmethod
    def normalize_provider(cls, value: str) -> str:
        if not value.strip():
            raise ValueError("must not be empty")
        return value.strip().lower()

    @field_validator("model")
    @classmethod
    def validate_model(cls, value: str) -> str:
        if not value.strip():
            raise ValueError("must not be empty")
        return value.strip()

    @field_validator("ollama_base_url")
    @classmethod
    def validate_ollama_base_url(cls, value: str | None) -> str | None:
        if value is None:
            return None

        normalized = value.strip().rstrip("/")
        if not normalized:
            raise ValueError("must be a valid HTTP or HTTPS base URL")
        try:
            parsed = urlsplit(normalized)
            # Accessing port validates invalid port forms too (for example :not-a-port).
            _ = parsed.port
        except ValueError:
            raise ValueError("must be a valid HTTP or HTTPS base URL") from None

        if (
            parsed.scheme not in {"http", "https"}
            or not parsed.hostname
            or parsed.username is not None
            or parsed.password is not None
            or parsed.query
            or parsed.fragment
        ):
            raise ValueError("must be a valid HTTP or HTTPS base URL")
        return normalized

    @classmethod
    def from_environment(cls, environment: Mapping[str, str] | None = None) -> "LlmSettings":
        source = os.environ if environment is None else environment
        provider = source.get("LLM_PROVIDER", "fake")
        shared_model = source.get("LLM_MODEL")
        # LLM_MODEL is canonical. The already documented OLLAMA_MODEL remains a fallback for
        # local backward compatibility when Ollama is selected and LLM_MODEL is absent.
        model = shared_model
        if model is None:
            model = (
                source.get("OLLAMA_MODEL")
                if provider.strip().lower() == "ollama"
                else "fake-structured-v1"
            )

        values = {
            "provider": provider,
            "model": model,
            "timeout_seconds": source.get("LLM_TIMEOUT_SECONDS", "5"),
            "ollama_base_url": source.get("OLLAMA_BASE_URL"),
        }
        try:
            settings = cls.model_validate(values)
        except ValidationError:
            # Environment values can become secrets in a future provider configuration. Do not
            # preserve Pydantic's value-bearing validation error as cause or context.
            settings = None

        if settings is None:
            raise LlmConfigurationError()

        if settings.provider not in {"fake", "ollama"}:
            raise LlmConfigurationError()
        if settings.provider == "ollama" and settings.ollama_base_url is None:
            raise LlmConfigurationError()
        return settings

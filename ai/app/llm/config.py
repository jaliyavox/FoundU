"""Environment-backed configuration for the shared LLM boundary.

Only the deterministic fake provider is available in Phase 1. Future adapters may add a provider
selection without changing agent call sites.
"""

import os
from collections.abc import Mapping

from pydantic import BaseModel, ConfigDict, Field, ValidationError, field_validator

from app.llm.errors import LlmConfigurationError


class LlmSettings(BaseModel):
    """Non-secret settings used to compose the current LLM client."""

    model_config = ConfigDict(extra="forbid")

    provider: str = "fake"
    model: str = "fake-structured-v1"
    timeout_seconds: int = Field(default=5, ge=1, le=120)

    @field_validator("provider", "model")
    @classmethod
    def validate_non_empty(cls, value: str) -> str:
        if not value.strip():
            raise ValueError("must not be empty")
        return value.strip()

    @classmethod
    def from_environment(cls, environment: Mapping[str, str] | None = None) -> "LlmSettings":
        source = os.environ if environment is None else environment
        values = {
            "provider": source.get("LLM_PROVIDER", "fake"),
            "model": source.get("LLM_MODEL", "fake-structured-v1"),
            "timeout_seconds": source.get("LLM_TIMEOUT_SECONDS", "5"),
        }
        try:
            settings = cls.model_validate(values)
        except ValidationError:
            # Environment values can become secrets in a future provider configuration. Do not
            # preserve Pydantic's value-bearing validation error as cause or context.
            settings = None

        if settings is None:
            raise LlmConfigurationError()

        # Phase 1 intentionally has no network provider. Fail closed rather than silently
        # treating a future provider setting as a fake client.
        if settings.provider != "fake":
            raise LlmConfigurationError()
        return settings

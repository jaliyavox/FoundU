"""Environment-backed configuration for the shared LLM boundary.

The fake, Ollama, Groq and Hugging Face providers share this contract. Provider adapters remain
behind the common factory so agents never need provider-specific configuration.
"""

import os
from collections.abc import Mapping
from urllib.parse import urlsplit

from pydantic import BaseModel, ConfigDict, Field, SecretStr, ValidationError, field_validator

from app.llm.errors import LlmConfigurationError

HUGGINGFACE_ROUTER_URL = "https://router.huggingface.co/v1"
GROQ_API_URL = "https://api.groq.com/openai/v1"

# Hosted providers that speak the OpenAI chat-completions API, and where each lives by default.
CHAT_COMPLETIONS_PROVIDERS = {"huggingface": HUGGINGFACE_ROUTER_URL, "groq": GROQ_API_URL}


class LlmSettings(BaseModel):
    """Settings used to compose the current LLM client.

    The only secret, the hosted provider's API key, is a SecretStr: it prints as asterisks and
    never appears in a repr, a validation error or a log line. (The field names say
    "huggingface" for history; they serve every chat-completions provider.)
    """

    model_config = ConfigDict(extra="forbid")

    provider: str = "fake"
    model: str = "fake-structured-v1"
    timeout_seconds: int = Field(default=5, ge=1, le=120)
    ollama_base_url: str | None = None
    huggingface_base_url: str = HUGGINGFACE_ROUTER_URL
    huggingface_token: SecretStr | None = None

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

    @field_validator("huggingface_base_url")
    @classmethod
    def validate_huggingface_base_url(cls, value: str) -> str:
        validated = cls.validate_ollama_base_url(value)
        if validated is None or not validated.startswith("https://"):
            raise ValueError("must be an HTTPS base URL")
        return validated

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
            # LLM_BASE_URL / LLM_API_KEY are canonical; the HF_ names still work.
            "huggingface_base_url": (
                source.get("LLM_BASE_URL")
                or source.get("HF_BASE_URL")
                or CHAT_COMPLETIONS_PROVIDERS.get(provider.strip().lower(), HUGGINGFACE_ROUTER_URL)
            ),
            "huggingface_token": source.get("LLM_API_KEY") or source.get("HF_TOKEN") or None,
        }
        try:
            settings = cls.model_validate(values)
        except ValidationError:
            # Environment values can become secrets in a future provider configuration. Do not
            # preserve Pydantic's value-bearing validation error as cause or context.
            settings = None

        if settings is None:
            raise LlmConfigurationError()

        if settings.provider not in {"fake", "ollama", *CHAT_COMPLETIONS_PROVIDERS}:
            raise LlmConfigurationError()
        if settings.provider == "ollama" and settings.ollama_base_url is None:
            raise LlmConfigurationError()
        if settings.provider in CHAT_COMPLETIONS_PROVIDERS and (
            settings.huggingface_token is None
            or not settings.huggingface_token.get_secret_value().strip()
        ):
            raise LlmConfigurationError()
        return settings

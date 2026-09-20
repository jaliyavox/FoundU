"""Provider-neutral structured generation contracts for FoundU agents.

Phase 1 deliberately exposes only a deterministic fake implementation. Real provider adapters
belong behind the same interface in a later, separately approved phase.
"""

from app.llm.client import LlmClient, create_llm_client
from app.llm.config import LlmSettings
from app.llm.errors import (
    LlmConfigurationError,
    LlmProviderError,
    LlmStructuredOutputError,
    LlmTimeoutError,
)
from app.llm.fake import FakeLlmClient
from app.llm.models import StructuredGenerationRequest
from app.llm.ollama import OllamaLlmClient

__all__ = [
    "FakeLlmClient",
    "LlmClient",
    "LlmConfigurationError",
    "LlmProviderError",
    "LlmSettings",
    "LlmStructuredOutputError",
    "LlmTimeoutError",
    "OllamaLlmClient",
    "StructuredGenerationRequest",
    "create_llm_client",
]

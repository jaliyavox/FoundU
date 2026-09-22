"""Shared LLM composition seam used by future agents."""

from typing import Protocol, TypeVar

from pydantic import BaseModel

from app.llm.config import LlmSettings
from app.llm.errors import LlmConfigurationError
from app.llm.fake import FakeLlmClient
from app.llm.models import StructuredGenerationRequest
from app.llm.ollama import OllamaLlmClient

StructuredOutput = TypeVar("StructuredOutput", bound=BaseModel)


class LlmClient(Protocol):
    """Provider-neutral, synchronous structured-generation interface.

    The existing FastAPI handlers and LangGraph nodes are synchronous, so Phase 1 remains
    synchronous. A later provider adapter can manage its own transport without changing agents.
    """

    def generate_structured(
        self,
        request: StructuredGenerationRequest,
        response_model: type[StructuredOutput],
    ) -> StructuredOutput:
        """Return an instance validated against the requested Pydantic model."""


def create_llm_client(settings: LlmSettings) -> LlmClient:
    """Compose the one approved client for this runtime.

    This is the single provider composition point, so agents never instantiate provider SDK
    clients themselves.
    """

    if settings.provider == "fake":
        return FakeLlmClient()
    if settings.provider == "ollama":
        return OllamaLlmClient(settings)
    raise LlmConfigurationError()

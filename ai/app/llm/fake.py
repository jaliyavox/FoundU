"""Deterministic, network-free structured-generation fake for tests and local composition."""

from collections import deque
from collections.abc import Iterable
from dataclasses import dataclass
from enum import StrEnum
from typing import Any, TypeVar

from pydantic import BaseModel, ValidationError

from app.llm.errors import LlmProviderError, LlmStructuredOutputError, LlmTimeoutError
from app.llm.models import StructuredGenerationRequest

StructuredOutput = TypeVar("StructuredOutput", bound=BaseModel)


class FakeResponseKind(StrEnum):
    SUCCESS = "success"
    MALFORMED = "malformed"
    PROVIDER_FAILURE = "provider_failure"
    TIMEOUT = "timeout"


@dataclass(frozen=True)
class FakeResponse:
    """One queued deterministic outcome; raw values still undergo Pydantic validation."""

    kind: FakeResponseKind
    value: Any = None


class FakeLlmClient:
    """Queue-based fake that never performs network I/O or retains generation requests."""

    def __init__(self, responses: Iterable[FakeResponse] | None = None) -> None:
        self._responses: deque[FakeResponse] = deque(responses or [])

    def queue_response(self, value: Any) -> None:
        self._responses.append(FakeResponse(FakeResponseKind.SUCCESS, value))

    def queue_malformed_response(self, value: Any) -> None:
        self._responses.append(FakeResponse(FakeResponseKind.MALFORMED, value))

    def queue_provider_failure(self) -> None:
        self._responses.append(FakeResponse(FakeResponseKind.PROVIDER_FAILURE))

    def queue_timeout(self) -> None:
        self._responses.append(FakeResponse(FakeResponseKind.TIMEOUT))

    def generate_structured(
        self,
        request: StructuredGenerationRequest,
        response_model: type[StructuredOutput],
    ) -> StructuredOutput:
        # Keep the signature aligned with LlmClient while intentionally not retaining `request`.
        del request
        if not self._responses:
            raise LlmProviderError()

        response = self._responses.popleft()
        if response.kind is FakeResponseKind.PROVIDER_FAILURE:
            raise LlmProviderError()
        if response.kind is FakeResponseKind.TIMEOUT:
            raise LlmTimeoutError()

        try:
            return response_model.model_validate(response.value)
        except ValidationError:
            # Do not raise inside this handler: a Pydantic ValidationError can retain the raw
            # provider value. Leaving the handler before raising the safe boundary error keeps
            # it out of both exception cause and context chains.
            pass

        # Both explicitly malformed and accidentally schema-invalid fake output take the exact
        # production-facing invalid-structured-output path.
        raise LlmStructuredOutputError()

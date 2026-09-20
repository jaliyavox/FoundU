"""Ollama adapter for the provider-neutral structured-generation interface."""

import json
from typing import Any, TypeVar

import httpx
from pydantic import BaseModel, ValidationError

from app.llm.config import LlmSettings
from app.llm.errors import (
    LlmConfigurationError,
    LlmProviderError,
    LlmStructuredOutputError,
    LlmTimeoutError,
)
from app.llm.models import StructuredGenerationRequest

StructuredOutput = TypeVar("StructuredOutput", bound=BaseModel)


class OllamaLlmClient:
    """Synchronous `/api/chat` adapter that returns only schema-validated Pydantic models.

    Requests and raw responses stay in method-local variables. This client does not log, cache,
    or expose them.
    """

    def __init__(self, settings: LlmSettings, transport: httpx.BaseTransport | None = None) -> None:
        if settings.provider != "ollama" or settings.ollama_base_url is None:
            raise LlmProviderError()
        self._model = settings.model
        try:
            http_client = httpx.Client(
                base_url=settings.ollama_base_url,
                timeout=settings.timeout_seconds,
                transport=transport,
            )
        except (httpx.HTTPError, ValueError):
            http_client = None
        if http_client is None:
            raise LlmConfigurationError()
        self._client = http_client

    def close(self) -> None:
        """Close the owned HTTP client when application composition shuts down."""

        self._client.close()

    def __enter__(self) -> "OllamaLlmClient":
        return self

    def __exit__(self, *_: object) -> None:
        self.close()

    def generate_structured(
        self,
        request: StructuredGenerationRequest,
        response_model: type[StructuredOutput],
    ) -> StructuredOutput:
        payload = {
            "model": self._model,
            "messages": [
                {"role": "system", "content": request.system_instruction},
                {"role": "user", "content": self._user_content(request.input)},
            ],
            "stream": False,
            "format": response_model.model_json_schema(),
            "options": {"temperature": 0},
            "think": False,
        }

        try:
            response = self._client.post("/api/chat", json=payload)
        except httpx.TimeoutException:
            failure = "timeout"
        except httpx.HTTPError:
            failure = "provider"
        else:
            failure = None

        if failure == "timeout":
            raise LlmTimeoutError()
        if failure == "provider":
            raise LlmProviderError()
        if not response.is_success:
            raise LlmProviderError()

        try:
            envelope: Any = response.json()
        except (json.JSONDecodeError, ValueError):
            envelope = None
        if not isinstance(envelope, dict):
            raise LlmProviderError()

        content = self._extract_content(envelope)
        if content is None:
            raise LlmProviderError()

        try:
            raw_output = json.loads(content, parse_constant=self._reject_json_constant)
        except (json.JSONDecodeError, ValueError):
            raw_output = None
        if raw_output is None:
            raise LlmStructuredOutputError()

        try:
            return response_model.model_validate(raw_output)
        except ValidationError:
            # Leave the validation handler before raising to prevent raw model output from being
            # retained in a cause or context chain.
            pass
        raise LlmStructuredOutputError()

    @staticmethod
    def _user_content(input_value: str | dict[str, Any]) -> str:
        if isinstance(input_value, str):
            return input_value
        return json.dumps(input_value, ensure_ascii=False, separators=(",", ":"))

    @staticmethod
    def _extract_content(envelope: dict[str, Any]) -> str | None:
        message = envelope.get("message")
        if not isinstance(message, dict):
            return None
        content = message.get("content")
        return content if isinstance(content, str) and content.strip() else None

    @staticmethod
    def _reject_json_constant(_: str) -> Any:
        raise ValueError("Non-standard JSON constant.")

"""Chat-completions adapter for hosted models: Groq, and Hugging Face Inference Providers.

Both speak the OpenAI chat-completions API (Hugging Face's router forwards each request to a
provider hosting the chosen model). Providers differ in one way that matters here: some can
enforce a JSON schema on the reply and some cannot. So the schema is always written into the
instructions, enforcement is asked for as well, and a provider that refuses the enforcement is
asked once more without it. Whatever comes back is validated against the schema before any
agent sees it - an unenforced reply that does not fit is refused like any other.
"""

import json
import re
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

# A reply wrapped in a markdown fence - common from models told to answer in JSON.
_FENCE = re.compile(r"^```(?:json)?\s*(.*?)\s*```$", re.DOTALL)


class HuggingFaceLlmClient:
    """Synchronous chat-completions adapter that returns only schema-validated models.

    Requests, raw responses and the token stay in method-local variables or the HTTP client's
    headers. This client does not log, cache or expose any of them.
    """

    def __init__(self, settings: LlmSettings, transport: httpx.BaseTransport | None = None) -> None:
        token = settings.huggingface_token
        if settings.provider not in {"huggingface", "groq"} or token is None:
            raise LlmProviderError()
        self._model = settings.model
        try:
            http_client = httpx.Client(
                base_url=settings.huggingface_base_url,
                timeout=settings.timeout_seconds,
                transport=transport,
                headers={"Authorization": f"Bearer {token.get_secret_value()}"},
            )
        except (httpx.HTTPError, ValueError):
            http_client = None
        if http_client is None:
            raise LlmConfigurationError()
        self._client = http_client

    def close(self) -> None:
        """Close the owned HTTP client when application composition shuts down."""

        self._client.close()

    def __enter__(self) -> "HuggingFaceLlmClient":
        return self

    def __exit__(self, *_: object) -> None:
        self.close()

    def generate_structured(
        self,
        request: StructuredGenerationRequest,
        response_model: type[StructuredOutput],
    ) -> StructuredOutput:
        schema = response_model.model_json_schema()
        payload: dict[str, Any] = {
            "model": self._model,
            "messages": [
                {
                    "role": "system",
                    "content": (
                        f"{request.system_instruction}\n\n"
                        "Reply with one JSON object and nothing else - no prose, no markdown. "
                        "It must match this JSON Schema:\n"
                        f"{json.dumps(schema, separators=(',', ':'))}"
                    ),
                },
                {"role": "user", "content": self._user_content(request.input)},
            ],
            "temperature": 0,
            "max_tokens": 512,
            "stream": False,
            "response_format": {
                "type": "json_schema",
                "json_schema": {"name": "result", "schema": schema},
            },
        }

        response = self._post(payload)
        if response.status_code in (400, 422):
            # This provider cannot enforce a schema. The instructions still carry it, and the
            # reply is still validated below.
            payload.pop("response_format")
            response = self._post(payload)
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

        raw_output = self._parse_json_object(content)
        if raw_output is None:
            raise LlmStructuredOutputError()

        try:
            return response_model.model_validate(raw_output)
        except ValidationError:
            # Leave the validation handler before raising to prevent raw model output from being
            # retained in a cause or context chain.
            pass
        raise LlmStructuredOutputError()

    def _post(self, payload: dict[str, Any]) -> httpx.Response:
        try:
            return self._client.post("/chat/completions", json=payload)
        except httpx.TimeoutException:
            failure = "timeout"
        except httpx.HTTPError:
            failure = "provider"
        if failure == "timeout":
            raise LlmTimeoutError()
        raise LlmProviderError()

    @staticmethod
    def _user_content(input_value: str | dict[str, Any]) -> str:
        if isinstance(input_value, str):
            return input_value
        return json.dumps(input_value, ensure_ascii=False, separators=(",", ":"))

    @staticmethod
    def _extract_content(envelope: dict[str, Any]) -> str | None:
        choices = envelope.get("choices")
        if not isinstance(choices, list) or not choices or not isinstance(choices[0], dict):
            return None
        message = choices[0].get("message")
        if not isinstance(message, dict):
            return None
        content = message.get("content")
        return content if isinstance(content, str) and content.strip() else None

    @classmethod
    def _parse_json_object(cls, content: str) -> Any:
        text = content.strip()
        fenced = _FENCE.match(text)
        if fenced:
            text = fenced.group(1)
        try:
            value = json.loads(text, parse_constant=cls._reject_json_constant)
        except (json.JSONDecodeError, ValueError):
            value = None
        if isinstance(value, dict):
            return value
        # A sentence around the object: take the outermost braces, once.
        start, end = text.find("{"), text.rfind("}")
        if start == -1 or end <= start:
            return None
        try:
            value = json.loads(text[start : end + 1], parse_constant=cls._reject_json_constant)
        except (json.JSONDecodeError, ValueError):
            return None
        return value if isinstance(value, dict) else None

    @staticmethod
    def _reject_json_constant(_: str) -> Any:
        raise ValueError("Non-standard JSON constant.")

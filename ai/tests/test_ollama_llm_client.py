"""Mocked contract tests for the Ollama structured-output adapter."""

import json

import httpx
import pytest
from pydantic import BaseModel, ConfigDict, Field

from app.llm import (
    FakeLlmClient,
    LlmConfigurationError,
    LlmProviderError,
    LlmSettings,
    LlmStructuredOutputError,
    LlmTimeoutError,
    OllamaLlmClient,
    StructuredGenerationRequest,
    create_llm_client,
)

SECRET_OUTPUT = "SECRET-OLLAMA-OUTPUT-DO-NOT-LEAK"
SECRET_PROMPT = "SECRET-PROMPT-DO-NOT-LEAK"


class StrictReply(BaseModel):
    model_config = ConfigDict(extra="forbid")

    answer: str
    confidence: int = Field(strict=True)


def request() -> StructuredGenerationRequest:
    return StructuredGenerationRequest(
        operation="provider_contract_test",
        system_instruction="Return only the supplied JSON schema.",
        input=SECRET_PROMPT,
        correlation_id="ollama-test-correlation",
    )


def settings(**overrides: object) -> LlmSettings:
    return LlmSettings(
        provider="ollama",
        model="test-model",
        timeout_seconds=7,
        ollama_base_url="http://ollama.test",
        **overrides,
    )


def adapter(handler: httpx.SyncByteStream | object) -> OllamaLlmClient:
    return OllamaLlmClient(settings(), transport=httpx.MockTransport(handler))  # type: ignore[arg-type]


def test_ollama_success_uses_chat_schema_non_streaming_and_configured_model() -> None:
    observed: dict[str, object] = {}

    def handler(http_request: httpx.Request) -> httpx.Response:
        observed["url"] = str(http_request.url)
        observed["body"] = json.loads(http_request.content)
        return httpx.Response(
            200,
            json={
                "message": {"role": "assistant", "content": '{"answer":"safe","confidence":1}'},
                "done": True,
            },
        )

    with adapter(handler) as client:
        result = client.generate_structured(request(), StrictReply)

    assert isinstance(result, StrictReply)
    assert result.answer == "safe"
    assert observed["url"] == "http://ollama.test/api/chat"
    body = observed["body"]
    assert isinstance(body, dict)
    assert body["model"] == "test-model"
    assert body["stream"] is False
    assert body["think"] is False
    assert body["options"] == {"temperature": 0}
    assert body["format"] == StrictReply.model_json_schema()
    assert body["messages"] == [
        {"role": "system", "content": "Return only the supplied JSON schema."},
        {"role": "user", "content": SECRET_PROMPT},
    ]


@pytest.mark.parametrize(
    "content",
    [
        "not json",
        '{"confidence":1}',
        '{"answer":"safe","confidence":"1"}',
        '{"answer":"safe","confidence":1,"unexpected":"field"}',
    ],
)
def test_ollama_rejects_invalid_or_schema_nonconforming_content(content: str) -> None:
    def handler(_: httpx.Request) -> httpx.Response:
        return httpx.Response(200, json={"message": {"content": content}})

    with adapter(handler) as client, pytest.raises(LlmStructuredOutputError) as error:
        client.generate_structured(request(), StrictReply)

    assert error.value.__cause__ is None
    assert error.value.__context__ is None


@pytest.mark.parametrize(
    "envelope",
    [{}, {"message": {}}, {"message": {"content": ""}}, {"message": {"content": 7}}],
)
def test_ollama_rejects_missing_or_invalid_message_content(envelope: dict[str, object]) -> None:
    def handler(_: httpx.Request) -> httpx.Response:
        return httpx.Response(200, json=envelope)

    with adapter(handler) as client, pytest.raises(LlmProviderError) as error:
        client.generate_structured(request(), StrictReply)

    assert error.value.__cause__ is None
    assert error.value.__context__ is None


def test_ollama_maps_network_timeout_http_error_and_malformed_envelope_safely() -> None:
    def network_handler(_: httpx.Request) -> httpx.Response:
        raise httpx.ConnectError(SECRET_OUTPUT)

    def timeout_handler(_: httpx.Request) -> httpx.Response:
        raise httpx.ReadTimeout(SECRET_OUTPUT)

    def server_handler(_: httpx.Request) -> httpx.Response:
        return httpx.Response(500, text=SECRET_OUTPUT)

    def envelope_handler(_: httpx.Request) -> httpx.Response:
        return httpx.Response(200, text="not provider json")

    cases: list[tuple[object, type[Exception]]] = [
        (network_handler, LlmProviderError),
        (timeout_handler, LlmTimeoutError),
        (server_handler, LlmProviderError),
        (envelope_handler, LlmProviderError),
    ]
    for handler, error_type in cases:
        with adapter(handler) as client, pytest.raises(error_type) as error:
            client.generate_structured(request(), StrictReply)
        assert SECRET_OUTPUT not in str(error.value)
        assert SECRET_OUTPUT not in repr(error.value)
        assert SECRET_PROMPT not in str(error.value)
        assert error.value.__cause__ is None
        assert error.value.__context__ is None


def test_ollama_does_not_store_prompt_or_previous_request() -> None:
    def handler(_: httpx.Request) -> httpx.Response:
        return httpx.Response(
            200,
            json={"message": {"content": '{"answer":"safe","confidence":1}'}},
        )

    with adapter(handler) as client:
        client.generate_structured(request(), StrictReply)
        assert not hasattr(client, "last_request")
        assert not hasattr(client, "last_prompt")
        assert SECRET_PROMPT not in repr(client.__dict__)


def test_ollama_config_uses_canonical_model_and_backward_compatible_fallback() -> None:
    canonical = LlmSettings.from_environment(
        {
            "LLM_PROVIDER": "ollama",
            "LLM_MODEL": "canonical-model",
            "OLLAMA_MODEL": "legacy-model",
            "OLLAMA_BASE_URL": "http://ollama.test",
        }
    )
    fallback = LlmSettings.from_environment(
        {
            "LLM_PROVIDER": "ollama",
            "OLLAMA_MODEL": "legacy-model",
            "OLLAMA_BASE_URL": "http://ollama.test",
        }
    )
    assert canonical.model == "canonical-model"
    assert fallback.model == "legacy-model"


@pytest.mark.parametrize(
    "base_url",
    ["http://localhost:11434", "http://127.0.0.1:11434", "https://internal-ollama.example"],
)
def test_ollama_configuration_accepts_http_and_https_base_urls(base_url: str) -> None:
    loaded = LlmSettings.from_environment(
        {
            "LLM_PROVIDER": "ollama",
            "LLM_MODEL": "test-model",
            "OLLAMA_BASE_URL": base_url,
        }
    )
    assert loaded.ollama_base_url == base_url


@pytest.mark.parametrize(
    "environment",
    [
        {"LLM_PROVIDER": "ollama", "LLM_MODEL": "test-model"},
        {"LLM_PROVIDER": "ollama", "LLM_MODEL": "test-model", "OLLAMA_BASE_URL": " "},
        {"LLM_PROVIDER": "ollama", "OLLAMA_BASE_URL": "http://ollama.test", "LLM_MODEL": " "},
    ],
)
def test_ollama_configuration_requires_nonblank_base_url_and_model(
    environment: dict[str, str],
) -> None:
    with pytest.raises(LlmConfigurationError) as error:
        LlmSettings.from_environment(environment)
    assert error.value.__cause__ is None
    assert error.value.__context__ is None


@pytest.mark.parametrize(
    "base_url",
    [
        "not-a-url",
        "ftp://localhost:11434",
        "://broken",
        "SECRET-OLLAMA-BASE-URL-DO-NOT-LEAK",
    ],
)
def test_ollama_invalid_base_url_is_safe_and_does_not_retain_environment_value(
    base_url: str,
) -> None:
    with pytest.raises(LlmConfigurationError) as error:
        LlmSettings.from_environment(
            {
                "LLM_PROVIDER": "ollama",
                "LLM_MODEL": "test-model",
                "OLLAMA_BASE_URL": base_url,
            }
        )

    assert base_url not in str(error.value)
    assert base_url not in repr(error.value)
    assert error.value.__cause__ is None
    assert error.value.__context__ is None


def test_provider_factory_composes_fake_and_ollama_and_rejects_unknown() -> None:
    assert isinstance(create_llm_client(LlmSettings.from_environment({})), FakeLlmClient)
    ollama_client = create_llm_client(
        LlmSettings.from_environment(
            {
                "LLM_PROVIDER": "ollama",
                "LLM_MODEL": "test-model",
                "OLLAMA_BASE_URL": "http://ollama.test",
            }
        )
    )
    assert isinstance(ollama_client, OllamaLlmClient)
    ollama_client.close()
    with pytest.raises(LlmConfigurationError):
        LlmSettings.from_environment({"LLM_PROVIDER": "unsupported"})

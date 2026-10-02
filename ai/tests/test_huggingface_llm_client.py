"""Mocked contract tests for the Hugging Face Inference Providers adapter."""

import json

import httpx
import pytest
from pydantic import BaseModel, ConfigDict, Field

from app.llm import (
    HuggingFaceLlmClient,
    LlmConfigurationError,
    LlmProviderError,
    LlmSettings,
    LlmStructuredOutputError,
    LlmTimeoutError,
    StructuredGenerationRequest,
    create_llm_client,
)

TOKEN = "hf_SECRET-TOKEN-DO-NOT-LEAK"
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
    )


def settings() -> LlmSettings:
    return LlmSettings(
        provider="huggingface",
        model="Qwen/Qwen2.5-7B-Instruct",
        timeout_seconds=9,
        huggingface_token=TOKEN,
    )


def adapter(handler) -> HuggingFaceLlmClient:
    return HuggingFaceLlmClient(settings(), transport=httpx.MockTransport(handler))


def reply(content: str) -> httpx.Response:
    message = {"role": "assistant", "content": content}
    return httpx.Response(200, json={"choices": [{"message": message}]})


def test_a_request_carries_the_token_model_and_schema_and_returns_a_validated_model() -> None:
    seen: dict[str, object] = {}

    def handler(http_request: httpx.Request) -> httpx.Response:
        seen["url"] = str(http_request.url)
        seen["auth"] = http_request.headers["authorization"]
        seen["body"] = json.loads(http_request.content)
        return reply('{"answer":"safe","confidence":1}')

    with adapter(handler) as client:
        result = client.generate_structured(request(), StrictReply)

    assert result == StrictReply(answer="safe", confidence=1)
    assert seen["url"] == "https://router.huggingface.co/v1/chat/completions"
    assert seen["auth"] == f"Bearer {TOKEN}"
    body = seen["body"]
    assert isinstance(body, dict)
    assert body["model"] == "Qwen/Qwen2.5-7B-Instruct"
    assert body["temperature"] == 0
    assert body["stream"] is False
    assert body["response_format"]["type"] == "json_schema"
    # The schema is in the instructions too, for providers that cannot enforce it.
    assert '"confidence"' in body["messages"][0]["content"]
    assert body["messages"][1]["content"] == SECRET_PROMPT


def test_a_provider_that_cannot_enforce_a_schema_is_asked_again_without_it() -> None:
    bodies: list[dict] = []

    def handler(http_request: httpx.Request) -> httpx.Response:
        body = json.loads(http_request.content)
        bodies.append(body)
        if "response_format" in body:
            return httpx.Response(400, json={"error": "response_format not supported"})
        return reply('Sure! ```json\n{"answer":"ok","confidence":2}\n```')

    with adapter(handler) as client:
        result = client.generate_structured(request(), StrictReply)

    assert result.answer == "ok"
    assert len(bodies) == 2
    assert "response_format" not in bodies[1]


@pytest.mark.parametrize(
    "content",
    [
        '```json\n{"answer":"fenced","confidence":3}\n```',
        'Here you go: {"answer":"wrapped","confidence":4} - done.',
    ],
)
def test_replies_wrapped_in_a_fence_or_a_sentence_are_unwrapped(content: str) -> None:
    with adapter(lambda _: reply(content)) as client:
        assert client.generate_structured(request(), StrictReply).confidence in (3, 4)


@pytest.mark.parametrize(
    "content",
    [
        '{"answer":"x","confidence":"high"}',  # wrong type
        '{"answer":"x","confidence":1,"extra":true}',  # not in the schema
        "I cannot help with that.",
        '{"answer":"x","confidence":NaN}',
    ],
)
def test_a_reply_that_does_not_fit_the_schema_is_refused(content: str) -> None:
    with adapter(lambda _: reply(content)) as client, pytest.raises(LlmStructuredOutputError):
        client.generate_structured(request(), StrictReply)


def test_failures_are_safe_and_never_carry_the_token_or_prompt() -> None:
    def unauthorised(_: httpx.Request) -> httpx.Response:
        return httpx.Response(401, json={"error": f"bad token {TOKEN} for {SECRET_PROMPT}"})

    def slow(_: httpx.Request) -> httpx.Response:
        raise httpx.ReadTimeout("slow")

    with adapter(unauthorised) as client, pytest.raises(LlmProviderError) as error:
        client.generate_structured(request(), StrictReply)
    assert TOKEN not in str(error.value) and SECRET_PROMPT not in str(error.value)
    assert error.value.__cause__ is None

    with adapter(slow) as client, pytest.raises(LlmTimeoutError):
        client.generate_structured(request(), StrictReply)


def test_the_token_is_required_and_never_printed() -> None:
    with pytest.raises(LlmConfigurationError):
        LlmSettings.from_environment({"LLM_PROVIDER": "huggingface", "LLM_MODEL": "m"})

    configured = LlmSettings.from_environment(
        {"LLM_PROVIDER": "huggingface", "LLM_MODEL": "Qwen/Qwen2.5-7B-Instruct", "HF_TOKEN": TOKEN}
    )
    assert TOKEN not in repr(configured)
    assert TOKEN not in str(configured.model_dump())
    assert isinstance(create_llm_client(configured), HuggingFaceLlmClient)


def test_groq_uses_the_same_adapter_at_its_own_address() -> None:
    configured = LlmSettings.from_environment(
        {"LLM_PROVIDER": "groq", "LLM_MODEL": "openai/gpt-oss-20b", "LLM_API_KEY": TOKEN}
    )
    assert configured.huggingface_base_url == "https://api.groq.com/openai/v1"
    assert isinstance(create_llm_client(configured), HuggingFaceLlmClient)

    seen: dict[str, str] = {}

    def handler(http_request: httpx.Request) -> httpx.Response:
        seen["url"] = str(http_request.url)
        seen["auth"] = http_request.headers["authorization"]
        return reply('{"answer":"groq","confidence":5}')

    groq = HuggingFaceLlmClient(configured, transport=httpx.MockTransport(handler))
    assert groq.generate_structured(request(), StrictReply).answer == "groq"
    assert seen["url"] == "https://api.groq.com/openai/v1/chat/completions"
    assert seen["auth"] == f"Bearer {TOKEN}"

    with pytest.raises(LlmConfigurationError):
        LlmSettings.from_environment({"LLM_PROVIDER": "groq", "LLM_MODEL": "openai/gpt-oss-20b"})


def test_a_missing_key_names_the_setting_and_groq_api_key_is_accepted() -> None:
    with pytest.raises(LlmConfigurationError) as missing:
        LlmSettings.from_environment({"LLM_PROVIDER": "groq", "LLM_MODEL": "openai/gpt-oss-20b"})
    assert "LLM_API_KEY" in str(missing.value)

    configured = LlmSettings.from_environment(
        {"LLM_PROVIDER": "groq", "LLM_MODEL": "openai/gpt-oss-20b", "GROQ_API_KEY": TOKEN}
    )
    assert configured.huggingface_token is not None
    assert TOKEN not in str(LlmConfigurationError("hint"))

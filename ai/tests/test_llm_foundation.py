"""Contract tests for the shared provider-neutral LLM foundation."""

import pytest
from pydantic import BaseModel, ConfigDict, Field

from app.llm import (
    FakeLlmClient,
    LlmConfigurationError,
    LlmProviderError,
    LlmSettings,
    LlmStructuredOutputError,
    LlmTimeoutError,
    StructuredGenerationRequest,
    create_llm_client,
)


class StrictAnswer(BaseModel):
    model_config = ConfigDict(extra="forbid")

    label: str
    score: int = Field(strict=True)


def request(prompt: str = "Return one safe label.") -> StructuredGenerationRequest:
    return StructuredGenerationRequest(
        operation="test_operation",
        system_instruction="Return only the requested schema.",
        input=prompt,
        correlation_id="correlation-1",
    )


def test_default_configuration_composes_fake_without_secret() -> None:
    settings = LlmSettings.from_environment({})
    assert settings.provider == "fake"
    assert settings.model == "fake-structured-v1"
    assert settings.timeout_seconds == 5
    assert isinstance(create_llm_client(settings), FakeLlmClient)


@pytest.mark.parametrize(
    "environment",
    [
        {"LLM_PROVIDER": "ollama"},
        {"LLM_PROVIDER": " ", "LLM_MODEL": "model"},
        {"LLM_TIMEOUT_SECONDS": "0"},
        {"LLM_TIMEOUT_SECONDS": "not-a-number"},
    ],
)
def test_invalid_or_unavailable_configuration_fails_safely(environment: dict[str, str]) -> None:
    with pytest.raises(LlmConfigurationError) as error:
        LlmSettings.from_environment(environment)
    assert "ollama" not in str(error.value)
    assert "not-a-number" not in str(error.value)


def test_fake_returns_requested_typed_pydantic_model() -> None:
    fake = FakeLlmClient()
    fake.queue_response({"label": "safe", "score": 1})

    result = fake.generate_structured(request(), StrictAnswer)

    assert isinstance(result, StrictAnswer)
    assert result.label == "safe"
    assert result.score == 1


@pytest.mark.parametrize(
    "raw",
    [
        {"score": 1},
        {"label": "safe", "score": "1"},
        {"label": "safe", "score": 1, "unexpected": "field"},
    ],
)
def test_fake_rejects_invalid_structured_output(raw: dict[str, object]) -> None:
    fake = FakeLlmClient()
    fake.queue_malformed_response(raw)

    with pytest.raises(LlmStructuredOutputError):
        fake.generate_structured(request(), StrictAnswer)


def test_fake_can_simulate_provider_failure_and_timeout() -> None:
    fake = FakeLlmClient()
    fake.queue_provider_failure()
    fake.queue_timeout()

    with pytest.raises(LlmProviderError):
        fake.generate_structured(request(), StrictAnswer)
    with pytest.raises(LlmTimeoutError):
        fake.generate_structured(request(), StrictAnswer)


def test_fake_consumes_configured_responses_in_order() -> None:
    fake = FakeLlmClient()
    fake.queue_response({"label": "first", "score": 1})
    fake.queue_response({"label": "second", "score": 2})

    assert fake.generate_structured(request(), StrictAnswer).label == "first"
    assert fake.generate_structured(request(), StrictAnswer).label == "second"
    with pytest.raises(LlmProviderError):
        fake.generate_structured(request(), StrictAnswer)


def test_safe_errors_do_not_leak_prompt_or_fake_sensitive_output() -> None:
    secret = "SECRET-PROVIDER-OUTPUT-DO-NOT-LEAK"
    fake = FakeLlmClient()
    fake.queue_malformed_response({"label": secret})

    with pytest.raises(LlmStructuredOutputError) as error:
        fake.generate_structured(request(secret), StrictAnswer)
    assert secret not in str(error.value)
    assert secret not in repr(error.value)
    assert error.value.__cause__ is None
    assert error.value.__context__ is None


def test_invalid_configuration_does_not_retain_sensitive_value_in_exception_chain() -> None:
    secret = "SECRET-PROVIDER-OUTPUT-DO-NOT-LEAK"

    with pytest.raises(LlmConfigurationError) as error:
        LlmSettings.from_environment({"LLM_TIMEOUT_SECONDS": secret})

    assert secret not in str(error.value)
    assert secret not in repr(error.value)
    assert error.value.__cause__ is None
    assert error.value.__context__ is None


def test_shared_contract_has_no_reasoning_or_chain_of_thought_fields() -> None:
    fields = StructuredGenerationRequest.model_fields
    assert "reasoning" not in fields
    assert "chain_of_thought" not in fields
    assert "scratchpad" not in fields

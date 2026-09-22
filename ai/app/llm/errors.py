"""Safe errors emitted by the provider-neutral LLM boundary.

Messages intentionally never include prompts, provider bodies, credentials, or structured input.
"""


class LlmError(Exception):
    """Base error for safe structured-generation failures."""


class LlmConfigurationError(LlmError):
    """The selected LLM configuration cannot be composed in this runtime."""

    def __init__(self) -> None:
        super().__init__("LLM configuration is invalid.")


class LlmProviderError(LlmError):
    """The configured provider failed without exposing provider internals."""

    def __init__(self) -> None:
        super().__init__("Structured generation provider failed safely.")


class LlmTimeoutError(LlmError):
    """The provider exceeded the caller's configured timeout."""

    def __init__(self) -> None:
        super().__init__("Structured generation timed out.")


class LlmStructuredOutputError(LlmError):
    """A provider response could not satisfy the caller's requested schema."""

    def __init__(self) -> None:
        super().__init__("Structured generation returned invalid structured output.")

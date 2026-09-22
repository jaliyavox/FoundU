"""Typed provider-neutral request contracts.

These contracts deliberately contain no reasoning, scratchpad, chain-of-thought, credentials,
or provider raw-response fields.
"""

from typing import Annotated, Any

from pydantic import BaseModel, ConfigDict, Field, StringConstraints

NonEmptyText = Annotated[str, StringConstraints(strip_whitespace=True, min_length=1)]


class StructuredGenerationRequest(BaseModel):
    """A schema-bound generation request supplied by an agent to an LLM client.

    Callers are responsible for supplying only data their agent is permitted to send to a future
    provider. This shared layer never logs or persists this request.
    """

    model_config = ConfigDict(extra="forbid")

    operation: NonEmptyText
    system_instruction: NonEmptyText
    input: str | dict[str, Any]
    correlation_id: str | None = Field(default=None, max_length=128)

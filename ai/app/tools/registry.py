"""Central executable tool registry.

Agents and future models must request tools through ``ToolRegistry.execute``. They never receive
callables directly, and trusted graph context—not model supplied payload—selects the agent identity.
"""

import re
from collections.abc import Callable
from dataclasses import dataclass

from pydantic import BaseModel, ValidationError

from app.agents.models import AGENT_PERMISSIONS, AgentName
from app.tools.errors import (
    ToolExecutionError,
    ToolInputError,
    ToolNotFoundError,
    ToolOutputError,
    ToolPermissionError,
)
from app.tools.models import ToolExecutionContext

ToolHandler = Callable[[BaseModel, ToolExecutionContext], BaseModel | dict[str, object]]


@dataclass(frozen=True)
class ToolDefinition:
    """One stable executable tool and its strict boundary contracts."""

    name: str
    description: str
    input_model: type[BaseModel]
    output_model: type[BaseModel]
    handler: ToolHandler

    @property
    def allowed_agents(self) -> frozenset[AgentName]:
        """Derive tool metadata from the one authoritative agent allow-list."""
        return frozenset(
            permissions.agent
            for permissions in AGENT_PERMISSIONS.values()
            if self.name in permissions.allow_listed_tools
        )


@dataclass(frozen=True)
class ToolExecutionResult:
    """Validated output plus safe trace events for a successful execution."""

    output: BaseModel
    trace: tuple[str, str]


class ToolRegistry:
    """Execute registered tools with AGENT_PERMISSIONS as the single permission source."""

    def __init__(self) -> None:
        self._definitions: dict[str, ToolDefinition] = {}

    def register(self, definition: ToolDefinition) -> None:
        if not definition.name or definition.name in self._definitions:
            raise ValueError("Tool registration is invalid.")
        if not definition.allowed_agents:
            raise ValueError("Tool registration is not allow-listed.")
        self._definitions[definition.name] = definition

    def get(self, tool_name: str) -> ToolDefinition:
        candidate_name = self._safe_tool_name(tool_name)
        definition = self._definitions.get(candidate_name)
        if definition is None:
            raise ToolNotFoundError("unknown")
        return definition

    def execute(
        self,
        tool_name: str,
        context: ToolExecutionContext,
        raw_input: object,
    ) -> ToolExecutionResult:
        """Validate, authorize, execute, and validate again at one central boundary."""
        definition = self.get(tool_name)
        # Only registry-owned metadata may appear in permission checks or observability traces.
        canonical_tool_name = definition.name
        if not isinstance(context, ToolExecutionContext):
            # Callers must construct this from trusted graph/application state, never model input.
            raise ToolPermissionError(canonical_tool_name)
        if context.agent not in definition.allowed_agents:
            raise ToolPermissionError(canonical_tool_name)

        try:
            validated_input = definition.input_model.model_validate(raw_input)
        except ValidationError:
            input_is_invalid = True
        else:
            input_is_invalid = False
        if input_is_invalid:
            # Raise outside the handler so validation values cannot remain in a context chain.
            raise ToolInputError(canonical_tool_name)

        try:
            raw_output = definition.handler(validated_input, context)
        except Exception:
            execution_failed = True
        else:
            execution_failed = False
        if execution_failed:
            raise ToolExecutionError(canonical_tool_name)

        try:
            validated_output = definition.output_model.model_validate(raw_output)
        except ValidationError:
            output_is_invalid = True
        else:
            output_is_invalid = False
        if output_is_invalid:
            raise ToolOutputError(canonical_tool_name)

        return ToolExecutionResult(
            output=validated_output,
            trace=(
                f"tool:attempt:{canonical_tool_name}",
                f"tool:success:{canonical_tool_name}",
            ),
        )

    @staticmethod
    def _safe_tool_name(tool_name: object) -> str:
        """Return a bounded stable identifier, never echoing arbitrary selection text in traces."""
        if isinstance(tool_name, str) and re.fullmatch(
            r"[A-Za-z0-9][A-Za-z0-9_-]{0,63}", tool_name
        ):
            return tool_name
        return "unknown"

    def validate_declared_tools(self) -> None:
        """Fail fast if a string allow-list entry lacks an executable registration."""
        declared_tools = {
            tool_name
            for permissions in AGENT_PERMISSIONS.values()
            for tool_name in permissions.allow_listed_tools
        }
        if declared_tools != set(self._definitions):
            raise ValueError("Tool registry does not match declared permissions.")

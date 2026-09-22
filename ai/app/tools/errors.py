"""Safe errors for the constrained tool boundary.

Messages deliberately omit tool inputs, outputs, private evidence, and implementation details.
"""


class ToolError(Exception):
    trace_event: str
    trace: tuple[str, str]


class ToolNotFoundError(ToolError):
    def __init__(self, tool_name: str) -> None:
        self.trace_event = f"tool:denied:{tool_name}"
        self.trace = (f"tool:attempt:{tool_name}", self.trace_event)
        super().__init__("Requested tool is unavailable.")


class ToolPermissionError(ToolError):
    def __init__(self, tool_name: str) -> None:
        self.trace_event = f"tool:denied:{tool_name}"
        self.trace = (f"tool:attempt:{tool_name}", self.trace_event)
        super().__init__("Tool execution is not permitted for this agent.")


class ToolInputError(ToolError):
    def __init__(self, tool_name: str) -> None:
        self.trace_event = f"tool:failure:{tool_name}"
        self.trace = (f"tool:attempt:{tool_name}", self.trace_event)
        super().__init__("Tool input is invalid.")


class ToolExecutionError(ToolError):
    def __init__(self, tool_name: str) -> None:
        self.trace_event = f"tool:failure:{tool_name}"
        self.trace = (f"tool:attempt:{tool_name}", self.trace_event)
        super().__init__("Tool execution failed safely.")


class ToolOutputError(ToolError):
    def __init__(self, tool_name: str) -> None:
        self.trace_event = f"tool:failure:{tool_name}"
        self.trace = (f"tool:attempt:{tool_name}", self.trace_event)
        super().__init__("Tool output is invalid.")

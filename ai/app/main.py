"""FoundU AI service with the shared LangGraph agent foundation."""

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel

from app.agents.graph import agent_graph
from app.agents.models import AgentRunRequest, AgentRunResponse
from app.agents.state import create_initial_state

app = FastAPI(title="FoundU AI Service", version="0.1.0")


class HealthResponse(BaseModel):
    status: str
    service: str


@app.get("/health", response_model=HealthResponse)
def health() -> HealthResponse:
    return HealthResponse(status="ok", service="FoundU AI Service")


@app.post("/agents/run", response_model=AgentRunResponse)
def run_agent(request: AgentRunRequest) -> AgentRunResponse:
    initial_state = create_initial_state(request)

    try:
        result = agent_graph.invoke(initial_state)
    except Exception:
        raise HTTPException(status_code=500, detail="Agent workflow failed safely.") from None

    return AgentRunResponse(
        agent_run_id=result["agent_run_id"],
        agent=result["requested_agent"],
        status="completed",
        output=result["output"],
        trace=result["trace"],
    )

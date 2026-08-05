"""FoundU AI service — FastAPI skeleton.

The LangGraph agent graph (coordinator, reader, verifier, messenger) and the real
POST /agents/run contract land in Step 5 of the build plan. This skeleton exposes a
health check so the service and its CI wiring exist from day one.
"""

from fastapi import FastAPI
from pydantic import BaseModel

app = FastAPI(title="FoundU AI Service", version="0.1.0")


class HealthResponse(BaseModel):
    status: str
    service: str


@app.get("/health", response_model=HealthResponse)
def health() -> HealthResponse:
    return HealthResponse(status="ok", service="FoundU AI Service")

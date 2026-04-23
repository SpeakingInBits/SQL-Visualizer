from __future__ import annotations

from fastapi import APIRouter, HTTPException
from pydantic import BaseModel

from db.executor import run_statement, visualize_query
from db.connector import require_active

router = APIRouter(tags=["query"])


class QueryPayload(BaseModel):
    sql: str


@router.post("/query/run")
def run_query(payload: QueryPayload):
    try:
        require_active()
        return run_statement(payload.sql)
    except Exception as exc:
        raise HTTPException(status_code=400, detail=str(exc))


@router.post("/query/visualize")
def visualize(payload: QueryPayload):
    try:
        require_active()
        return visualize_query(payload.sql)
    except Exception as exc:
        raise HTTPException(status_code=400, detail=str(exc))

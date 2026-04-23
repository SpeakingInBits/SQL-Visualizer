from __future__ import annotations

from fastapi import APIRouter, HTTPException
from pydantic import BaseModel

import db.connector as connector
import seed

router = APIRouter(tags=["connection"])


class SQLServerPayload(BaseModel):
    host: str
    port: int = 1433
    database: str
    username: str = ""
    password: str = ""
    trusted: bool = False


class SQLitePayload(BaseModel):
    path: str


@router.post("/connect/sqlserver")
def connect_sqlserver(payload: SQLServerPayload):
    try:
        conn = connector.connect_sqlserver(
            host=payload.host,
            port=payload.port,
            database=payload.database,
            username=payload.username,
            password=payload.password,
            trusted=payload.trusted,
        )
        return {"ok": True, "display_name": conn.display_name, "engine": conn.engine}
    except Exception as exc:
        raise HTTPException(status_code=400, detail=str(exc))


@router.post("/connect/sqlite")
def connect_sqlite(payload: SQLitePayload):
    try:
        conn = connector.connect_sqlite(payload.path)
        return {"ok": True, "display_name": conn.display_name, "engine": conn.engine}
    except Exception as exc:
        raise HTTPException(status_code=400, detail=str(exc))


@router.delete("/disconnect")
def disconnect():
    connector.disconnect()
    return {"ok": True}


@router.get("/status")
def status():
    conn = connector.get_active()
    if conn is None:
        return {"connected": False}
    return {
        "connected": True,
        "engine": conn.engine,
        "display_name": conn.display_name,
    }


@router.get("/samples")
def list_samples():
    """Return the pre-built sample SQLite databases."""
    return seed.list_samples()

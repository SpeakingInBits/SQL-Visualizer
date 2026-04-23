from __future__ import annotations

import re
from pathlib import Path

from fastapi import APIRouter, HTTPException, UploadFile, File, Form, Query
from fastapi.responses import JSONResponse

import db.script_store as store
from db.connector import require_active, get_active

router = APIRouter(tags=["scripts"])

GO_SPLITTER = re.compile(r"^\s*GO\s*$", re.IGNORECASE | re.MULTILINE)


def _split_statements(sql: str, engine: str) -> list[str]:
    """Split a multi-statement script into individual runnable statements."""
    if engine == "sqlserver":
        batches = GO_SPLITTER.split(sql)
    else:
        # SQLite: split on semicolons, keep semicolons out
        batches = [s for s in sql.split(";") if s.strip()]

    return [b.strip() for b in batches if b.strip()]


@router.get("/scripts")
def list_scripts():
    return store.list_scripts()


@router.post("/scripts/upload")
async def upload_script(
    file: UploadFile = File(...),
    engine_hint: str = Form(default=""),
):
    if not file.filename or not file.filename.lower().endswith(".sql"):
        raise HTTPException(status_code=400, detail="Only .sql files are accepted.")
    content = await file.read()
    if len(content) > 10 * 1024 * 1024:  # 10 MB sanity limit
        raise HTTPException(status_code=400, detail="Script file exceeds 10 MB limit.")
    entry = store.save_script(file.filename, content, engine_hint=engine_hint)
    return entry


@router.delete("/scripts/{script_id}")
def delete_script(script_id: str):
    deleted = store.delete_script(script_id)
    if not deleted:
        raise HTTPException(status_code=404, detail="Script not found.")
    return {"ok": True}


@router.post("/scripts/{script_id}/run")
def run_script(script_id: str):
    entry = store.get_script(script_id)
    if entry is None:
        raise HTTPException(status_code=404, detail="Script not found.")

    conn = get_active()
    if conn is None:
        raise HTTPException(status_code=400, detail="No active database connection.")

    sql = Path(entry["path"]).read_text(encoding="utf-8", errors="replace")
    statements = _split_statements(sql, conn.engine)

    results = []
    for stmt in statements:
        try:
            cur = conn.cursor()
            cur.execute(stmt)
            if cur.description:
                rows = cur.fetchall()
                cols = [d[0] for d in cur.description]
                results.append({
                    "statement": stmt[:120],
                    "type": "SELECT",
                    "rows_affected": len(rows),
                    "ok": True,
                })
            else:
                conn.commit()
                results.append({
                    "statement": stmt[:120],
                    "type": "DML",
                    "rows_affected": cur.rowcount,
                    "ok": True,
                })
        except Exception as exc:
            results.append({
                "statement": stmt[:120],
                "type": "ERROR",
                "error": str(exc),
                "ok": False,
            })

    store.touch_script(script_id)
    return {"script_id": script_id, "results": results}

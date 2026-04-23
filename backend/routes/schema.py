from __future__ import annotations

from fastapi import APIRouter, HTTPException, Query

import db.schema as schema_mod
from db.connector import require_active

router = APIRouter(tags=["schema"])


@router.get("/databases")
def list_databases():
    try:
        return schema_mod.list_databases()
    except Exception as exc:
        raise HTTPException(status_code=400, detail=str(exc))


@router.get("/tables")
def list_tables(database: str | None = Query(default=None)):
    try:
        return schema_mod.list_tables(database=database)
    except Exception as exc:
        raise HTTPException(status_code=400, detail=str(exc))


@router.get("/columns")
def list_columns(
    table: str = Query(...),
    schema: str = Query(default="dbo"),
    database: str | None = Query(default=None),
):
    try:
        cols = schema_mod.list_columns(table=table, schema=schema, database=database)
        fks = schema_mod.list_foreign_keys(table=table, schema=schema, database=database)
        fk_cols = {fk["column"] for fk in fks}
        for col in cols:
            col["is_foreign_key"] = col["name"] in fk_cols
        return {"columns": cols, "foreign_keys": fks}
    except Exception as exc:
        raise HTTPException(status_code=400, detail=str(exc))

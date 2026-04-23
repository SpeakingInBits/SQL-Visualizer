"""
Schema introspection for SQL Server and SQLite.
Returns databases, tables, columns, primary keys, and foreign keys.
"""
from __future__ import annotations

from typing import Any

from db.connector import ActiveConnection, require_active


# ---------------------------------------------------------------------------
# Public API
# ---------------------------------------------------------------------------

def list_databases(conn: ActiveConnection | None = None) -> list[str]:
    conn = conn or require_active()
    if conn.engine == "sqlite":
        return ["main"]
    cur = conn.cursor()
    cur.execute("SELECT name FROM sys.databases WHERE state_desc = 'ONLINE' ORDER BY name")
    return [row[0] for row in cur.fetchall()]


def list_tables(database: str | None = None, conn: ActiveConnection | None = None) -> list[dict[str, str]]:
    conn = conn or require_active()
    if conn.engine == "sqlite":
        cur = conn.cursor()
        cur.execute(
            "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name"
        )
        return [{"schema": "", "name": row[0]} for row in cur.fetchall()]
    # SQL Server
    db = database or conn.database
    cur = conn.cursor()
    cur.execute(
        f"SELECT TABLE_SCHEMA, TABLE_NAME FROM [{db}].INFORMATION_SCHEMA.TABLES "
        "WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_SCHEMA, TABLE_NAME"
    )
    return [{"schema": row[0], "name": row[1]} for row in cur.fetchall()]


def list_columns(
    table: str,
    schema: str = "dbo",
    database: str | None = None,
    conn: ActiveConnection | None = None,
) -> list[dict[str, Any]]:
    conn = conn or require_active()
    if conn.engine == "sqlite":
        return _sqlite_columns(table, conn)
    return _sqlserver_columns(table, schema, database or conn.database, conn)


def list_foreign_keys(
    table: str,
    schema: str = "dbo",
    database: str | None = None,
    conn: ActiveConnection | None = None,
) -> list[dict[str, str]]:
    conn = conn or require_active()
    if conn.engine == "sqlite":
        return _sqlite_fks(table, conn)
    return _sqlserver_fks(table, schema, database or conn.database, conn)


# ---------------------------------------------------------------------------
# SQLite helpers
# ---------------------------------------------------------------------------

def _sqlite_columns(table: str, conn: ActiveConnection) -> list[dict[str, Any]]:
    cur = conn.cursor()
    cur.execute(f"PRAGMA table_info([{table}])")
    rows = cur.fetchall()
    pks = {row[1] for row in rows if row[5]}  # cid, name, type, notnull, dflt, pk
    return [
        {
            "name": row[1],
            "data_type": row[2] or "TEXT",
            "is_nullable": not row[3],
            "is_primary_key": row[1] in pks,
            "default": row[4],
        }
        for row in rows
    ]


def _sqlite_fks(table: str, conn: ActiveConnection) -> list[dict[str, str]]:
    cur = conn.cursor()
    cur.execute(f"PRAGMA foreign_key_list([{table}])")
    return [
        {
            "column": row[3],
            "ref_table": row[2],
            "ref_column": row[4],
        }
        for row in cur.fetchall()
    ]


# ---------------------------------------------------------------------------
# SQL Server helpers
# ---------------------------------------------------------------------------

def _sqlserver_columns(
    table: str, schema: str, database: str, conn: ActiveConnection
) -> list[dict[str, Any]]:
    cur = conn.cursor()
    cur.execute(
        f"""
        SELECT
            c.COLUMN_NAME,
            c.DATA_TYPE,
            c.IS_NULLABLE,
            c.COLUMN_DEFAULT,
            CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IS_PK
        FROM [{database}].INFORMATION_SCHEMA.COLUMNS c
        LEFT JOIN (
            SELECT kcu.COLUMN_NAME
            FROM [{database}].INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            JOIN [{database}].INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
              ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
             AND tc.TABLE_SCHEMA    = kcu.TABLE_SCHEMA
             AND tc.TABLE_NAME      = kcu.TABLE_NAME
            WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
              AND tc.TABLE_SCHEMA = ?
              AND tc.TABLE_NAME   = ?
        ) pk ON c.COLUMN_NAME = pk.COLUMN_NAME
        WHERE c.TABLE_SCHEMA = ?
          AND c.TABLE_NAME   = ?
        ORDER BY c.ORDINAL_POSITION
        """,
        (schema, table, schema, table),
    )
    return [
        {
            "name": row[0],
            "data_type": row[1],
            "is_nullable": row[2] == "YES",
            "default": row[3],
            "is_primary_key": bool(row[4]),
        }
        for row in cur.fetchall()
    ]


def _sqlserver_fks(
    table: str, schema: str, database: str, conn: ActiveConnection
) -> list[dict[str, str]]:
    cur = conn.cursor()
    cur.execute(
        f"""
        SELECT
            kcu.COLUMN_NAME,
            rcu.TABLE_NAME  AS REF_TABLE,
            rcu.COLUMN_NAME AS REF_COLUMN
        FROM [{database}].INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc
        JOIN [{database}].INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
          ON rc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
         AND kcu.TABLE_SCHEMA   = ?
         AND kcu.TABLE_NAME     = ?
        JOIN [{database}].INFORMATION_SCHEMA.KEY_COLUMN_USAGE rcu
          ON rc.UNIQUE_CONSTRAINT_NAME = rcu.CONSTRAINT_NAME
         AND rcu.ORDINAL_POSITION      = kcu.ORDINAL_POSITION
        ORDER BY kcu.ORDINAL_POSITION
        """,
        (schema, table),
    )
    return [
        {"column": row[0], "ref_table": row[1], "ref_column": row[2]}
        for row in cur.fetchall()
    ]

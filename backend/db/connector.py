"""
Connection manager.

Maintains a single active connection at a time.
Supports:
  - Microsoft SQL Server  (engine="sqlserver") via pyodbc
  - SQLite                (engine="sqlite")    via stdlib sqlite3
"""
from __future__ import annotations

import sqlite3
import threading
from dataclasses import dataclass, field
from typing import Any, Literal

try:
    import pyodbc
    HAS_PYODBC = True
except ImportError:
    HAS_PYODBC = False


EngineType = Literal["sqlserver", "sqlite"]

_lock = threading.Lock()


@dataclass
class ActiveConnection:
    engine: EngineType
    display_name: str
    # For SQL Server
    host: str = ""
    port: int = 1433
    database: str = ""
    username: str = ""
    # raw connection objects
    _conn: Any = field(default=None, repr=False)

    def cursor(self):
        return self._conn.cursor()

    def commit(self):
        self._conn.commit()

    def close(self):
        try:
            self._conn.close()
        except Exception:
            pass


_active: ActiveConnection | None = None


def _build_sqlserver_connstr(
    host: str, port: int, database: str, username: str, password: str, trusted: bool
) -> str:
    driver = _pick_odbc_driver()
    if trusted:
        return (
            f"DRIVER={{{driver}}};"
            f"SERVER={host},{port};"
            f"DATABASE={database};"
            "Trusted_Connection=yes;"
            "TrustServerCertificate=yes;"
        )
    return (
        f"DRIVER={{{driver}}};"
        f"SERVER={host},{port};"
        f"DATABASE={database};"
        f"UID={username};"
        f"PWD={password};"
        "TrustServerCertificate=yes;"
    )


def _pick_odbc_driver() -> str:
    """Return the best available SQL Server ODBC driver name."""
    if not HAS_PYODBC:
        raise RuntimeError("pyodbc is not installed.")
    available = [d for d in pyodbc.drivers() if "SQL Server" in d]
    if not available:
        raise RuntimeError(
            "No SQL Server ODBC driver found. "
            "Install 'ODBC Driver 18 for SQL Server' from Microsoft."
        )
    # Prefer highest version number
    available.sort(reverse=True)
    return available[0]


def connect_sqlserver(
    host: str,
    port: int,
    database: str,
    username: str,
    password: str,
    trusted: bool = False,
) -> ActiveConnection:
    global _active
    if not HAS_PYODBC:
        raise RuntimeError("pyodbc is not installed.")
    connstr = _build_sqlserver_connstr(host, port, database, username, password, trusted)
    with _lock:
        if _active is not None:
            _active.close()
        raw = pyodbc.connect(connstr, timeout=10)
        raw.autocommit = False
        label = f"{host}:{port}/{database}" if not trusted else f"{host}:{port}/{database} (Windows Auth)"
        _active = ActiveConnection(
            engine="sqlserver",
            display_name=label,
            host=host,
            port=port,
            database=database,
            username=username,
            _conn=raw,
        )
    return _active


def connect_sqlite(path: str) -> ActiveConnection:
    global _active
    with _lock:
        if _active is not None:
            _active.close()
        raw = sqlite3.connect(path, check_same_thread=False)
        raw.row_factory = sqlite3.Row
        _active = ActiveConnection(
            engine="sqlite",
            display_name=path,
            _conn=raw,
        )
    return _active


def disconnect() -> None:
    global _active
    with _lock:
        if _active is not None:
            _active.close()
            _active = None


def get_active() -> ActiveConnection | None:
    return _active


def require_active() -> ActiveConnection:
    conn = get_active()
    if conn is None:
        raise RuntimeError("No active database connection.")
    return conn

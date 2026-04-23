"""
Script store — persists .sql script metadata to app_data/scripts.json.
"""
from __future__ import annotations

import json
import uuid
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

APP_DATA_DIR = Path(__file__).parent.parent / "app_data"
SCRIPTS_DIR = APP_DATA_DIR / "scripts"
INDEX_FILE = APP_DATA_DIR / "scripts.json"


def _ensure_dirs() -> None:
    SCRIPTS_DIR.mkdir(parents=True, exist_ok=True)


def _load_index() -> list[dict[str, Any]]:
    _ensure_dirs()
    if not INDEX_FILE.exists():
        return []
    try:
        return json.loads(INDEX_FILE.read_text(encoding="utf-8"))
    except (json.JSONDecodeError, OSError):
        return []


def _save_index(index: list[dict[str, Any]]) -> None:
    _ensure_dirs()
    INDEX_FILE.write_text(json.dumps(index, indent=2, ensure_ascii=False), encoding="utf-8")


def list_scripts() -> list[dict[str, Any]]:
    return _load_index()


def save_script(filename: str, content: bytes, engine_hint: str = "") -> dict[str, Any]:
    """Persist a .sql file and add it to the index."""
    _ensure_dirs()
    script_id = str(uuid.uuid4())
    safe_name = Path(filename).name  # strip any path components
    dest = SCRIPTS_DIR / f"{script_id}_{safe_name}"
    dest.write_bytes(content)

    entry: dict[str, Any] = {
        "id": script_id,
        "name": safe_name,
        "engine_hint": engine_hint,
        "path": str(dest),
        "created_at": datetime.now(timezone.utc).isoformat(),
        "last_used": None,
    }
    index = _load_index()
    index.append(entry)
    _save_index(index)
    return entry


def delete_script(script_id: str) -> bool:
    index = _load_index()
    entry = next((e for e in index if e["id"] == script_id), None)
    if entry is None:
        return False
    try:
        Path(entry["path"]).unlink(missing_ok=True)
    except OSError:
        pass
    _save_index([e for e in index if e["id"] != script_id])
    return True


def get_script(script_id: str) -> dict[str, Any] | None:
    return next((e for e in _load_index() if e["id"] == script_id), None)


def touch_script(script_id: str) -> None:
    """Update last_used timestamp."""
    index = _load_index()
    for entry in index:
        if entry["id"] == script_id:
            entry["last_used"] = datetime.now(timezone.utc).isoformat()
    _save_index(index)

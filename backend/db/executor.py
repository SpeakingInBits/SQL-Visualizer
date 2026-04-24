"""
Query executor.

Runs any SQL statement and, for SELECT queries, produces structured
step-data that the frontend animates.

Step extraction strategy
------------------------
sqlparse tokenises the query to detect top-level clauses.  We then run
additional queries to gather the data needed for each visualisation:

  ORDER BY  → pre-sort rows  +  sorted rows  +  sort-key column index
  WHERE     → all rows  +  boolean match mask per row
  JOIN      → left-table rows, right-table rows, match-index pairs, merged rows
"""
from __future__ import annotations

import re
from typing import Any

import sqlparse
from sqlparse.sql import IdentifierList, Identifier, Where, Comparison
from sqlparse.tokens import Keyword, DML

from db.connector import ActiveConnection, require_active


MAX_VIZ_ROWS = 200


# ---------------------------------------------------------------------------
# Public helpers
# ---------------------------------------------------------------------------

def _rows_to_dicts(cursor, rows) -> list[dict[str, Any]]:
    cols = [d[0] for d in cursor.description]
    return [dict(zip(cols, row)) for row in rows]


def _col_meta(cursor) -> list[dict[str, str]]:
    return [{"name": d[0]} for d in cursor.description]


# ---------------------------------------------------------------------------
# Plain execution  (all statement types)
# ---------------------------------------------------------------------------

def run_statement(sql: str, conn: ActiveConnection | None = None) -> dict[str, Any]:
    """
    Execute one SQL statement.
    Returns {"columns": [...], "rows": [...], "rows_affected": int, "type": "SELECT"|"DML"}.
    """
    conn = conn or require_active()
    cur = conn.cursor()
    cur.execute(sql)

    stmt_type = _detect_stmt_type(sql)

    if stmt_type == "SELECT":
        rows = cur.fetchmany(MAX_VIZ_ROWS)
        result = {
            "type": "SELECT",
            "columns": _col_meta(cur),
            "rows": _rows_to_dicts(cur, rows),
            "rows_affected": None,
            "truncated": cur.fetchone() is not None,
        }
    else:
        conn.commit()
        result = {
            "type": "DML",
            "columns": [],
            "rows": [],
            "rows_affected": cur.rowcount,
            "truncated": False,
        }
    return result


# ---------------------------------------------------------------------------
# Visualise  (SELECT only)
# ---------------------------------------------------------------------------

def visualize_query(sql: str, conn: ActiveConnection | None = None) -> dict[str, Any]:
    """
    Run a SELECT and return step-data for animations.
    Falls back to a plain result if no visualisable clause is detected.
    """
    conn = conn or require_active()
    parsed = sqlparse.parse(sql.strip())[0]

    clauses = _detect_clauses(parsed)

    # Priority: JOIN > ORDER BY > WHERE  (a query may have several)
    if clauses["has_join"]:
        return _viz_join(sql, parsed, conn)
    if clauses["has_order_by"]:
        return _viz_order_by(sql, parsed, conn)
    if clauses["has_where"]:
        return _viz_where(sql, parsed, conn)

    # Plain SELECT — return tabular result
    result = run_statement(sql, conn)
    result["viz_type"] = "plain"
    return result


# ---------------------------------------------------------------------------
# Clause detection
# ---------------------------------------------------------------------------

def _detect_stmt_type(sql: str) -> str:
    first = sqlparse.parse(sql.strip())[0].get_type()
    return "SELECT" if first == "SELECT" else "DML"


def _detect_clauses(parsed) -> dict[str, bool]:
    flat = [str(t).upper() for t in parsed.flatten()]
    text_upper = parsed.value.upper()
    return {
        "has_where": "WHERE" in flat,
        "has_order_by": "ORDER" in flat and "BY" in flat,
        "has_join": bool(re.search(r"\bJOIN\b", text_upper)),
        "has_group_by": "GROUP" in flat and "BY" in flat,
    }


def _get_join_type(sql_upper: str) -> str:
    for jt in ("LEFT OUTER", "RIGHT OUTER", "FULL OUTER", "INNER", "LEFT", "RIGHT", "FULL", "CROSS"):
        if jt in sql_upper:
            return jt
    return "INNER"


# ---------------------------------------------------------------------------
# ORDER BY visualisation
# ---------------------------------------------------------------------------

def _viz_order_by(sql: str, parsed, conn: ActiveConnection) -> dict[str, Any]:
    # Strip ORDER BY to get unsorted rows
    sql_no_order = re.sub(r"\bORDER\s+BY\b.*$", "", sql, flags=re.IGNORECASE | re.DOTALL).strip()

    cur = conn.cursor()
    cur.execute(sql_no_order)
    unsorted_rows = _rows_to_dicts(cur, cur.fetchmany(MAX_VIZ_ROWS))
    columns = _col_meta(cur)

    cur.execute(sql)
    sorted_rows = _rows_to_dicts(cur, cur.fetchmany(MAX_VIZ_ROWS))

    # Find sort-key column names from ORDER BY clause
    order_match = re.search(r"\bORDER\s+BY\b(.*?)(?:LIMIT|OFFSET|$)", sql, re.IGNORECASE | re.DOTALL)
    sort_keys: list[str] = []
    if order_match:
        for part in order_match.group(1).split(","):
            col = re.sub(r"\b(ASC|DESC)\b", "", part, flags=re.IGNORECASE).strip().strip("[]\"'`")
            if col:
                sort_keys.append(col.split(".")[-1])  # strip table prefix

    sort_key_indices = [
        i for i, c in enumerate(columns) if c["name"] in sort_keys
    ]

    return {
        "viz_type": "order_by",
        "columns": columns,
        "unsorted_rows": unsorted_rows,
        "sorted_rows": sorted_rows,
        "sort_key_indices": sort_key_indices,
        "sort_keys": sort_keys,
    }


# ---------------------------------------------------------------------------
# WHERE visualisation
# ---------------------------------------------------------------------------

def _viz_where(sql: str, parsed, conn: ActiveConnection) -> dict[str, Any]:
    # Get all rows (remove WHERE clause)
    sql_no_where = re.sub(r"\bWHERE\b.*?(?=\bORDER\b|\bGROUP\b|\bHAVING\b|\bLIMIT\b|$)",
                          "", sql, flags=re.IGNORECASE | re.DOTALL).strip()

    cur = conn.cursor()
    cur.execute(sql_no_where)
    all_rows = _rows_to_dicts(cur, cur.fetchmany(MAX_VIZ_ROWS))
    columns = _col_meta(cur)

    # Extract WHERE clause text
    where_match = re.search(r"\bWHERE\b(.*?)(?=\bORDER\b|\bGROUP\b|\bHAVING\b|\bLIMIT\b|$)",
                            sql, re.IGNORECASE | re.DOTALL)
    where_text = where_match.group(1).strip() if where_match else ""

    # Split into individual AND sub-conditions for step-by-step visualization
    conditions = _split_where_conditions(where_text) if where_text else [where_text]

    # Base query for per-condition evaluation (no ORDER BY/GROUP BY needed)
    sql_base = re.sub(r"\bORDER\s+BY\b.*$", "", sql_no_where, flags=re.IGNORECASE | re.DOTALL).strip()
    all_row_keys = [_row_key(r) for r in all_rows]

    # Build per-row per-condition boolean matrix
    condition_results: list[list[bool]] = [[] for _ in all_rows]
    for cond in conditions:
        cond_sql = f"{sql_base} WHERE {cond}"
        cur.execute(cond_sql)
        cond_set = {_row_key(r) for r in _rows_to_dicts(cur, cur.fetchall())}
        for i, key in enumerate(all_row_keys):
            condition_results[i].append(key in cond_set)

    # Overall match mask: row passes if all sub-conditions pass
    match_mask = [all(condition_results[i]) for i in range(len(all_rows))]

    return {
        "viz_type": "where",
        "columns": columns,
        "all_rows": all_rows,
        "match_mask": match_mask,
        "where_text": where_text,
        "conditions": conditions,
        "condition_results": condition_results,
    }


def _split_where_conditions(where_text: str) -> list[str]:
    """Split a WHERE clause on top-level AND operators (respects parentheses)."""
    conditions: list[str] = []
    depth = 0
    start = 0
    i = 0
    upper = where_text.upper()
    while i < len(where_text):
        ch = where_text[i]
        if ch == '(':
            depth += 1
            i += 1
        elif ch == ')':
            depth -= 1
            i += 1
        elif depth == 0 and upper[i:i + 5] == ' AND ':
            conditions.append(where_text[start:i].strip())
            i += 5
            start = i
        else:
            i += 1
    last = where_text[start:].strip()
    if last:
        conditions.append(last)
    return conditions if conditions else [where_text]


def _row_key(row: dict) -> tuple:
    return tuple(str(v) for v in row.values())


# ---------------------------------------------------------------------------
# JOIN visualisation
# ---------------------------------------------------------------------------

def _viz_join(sql: str, parsed, conn: ActiveConnection) -> dict[str, Any]:
    sql_upper = sql.upper()
    join_type = _get_join_type(sql_upper)

    # Parse out table aliases and join condition
    join_info = _parse_join_info(sql)

    left_table = join_info.get("left_table", "")
    right_table = join_info.get("right_table", "")
    left_alias = join_info.get("left_alias", left_table)
    right_alias = join_info.get("right_alias", right_table)
    on_condition = join_info.get("on_condition", "")
    left_key = join_info.get("left_key", "")
    right_key = join_info.get("right_key", "")

    cur = conn.cursor()

    # Fetch left table rows
    if left_table:
        cur.execute(f"SELECT * FROM [{left_table}]")
        left_rows = _rows_to_dicts(cur, cur.fetchmany(MAX_VIZ_ROWS))
        left_columns = _col_meta(cur)
    else:
        left_rows, left_columns = [], []

    # Fetch right table rows
    if right_table:
        cur.execute(f"SELECT * FROM [{right_table}]")
        right_rows = _rows_to_dicts(cur, cur.fetchmany(MAX_VIZ_ROWS))
        right_columns = _col_meta(cur)
    else:
        right_rows, right_columns = [], []

    # Execute the original JOIN query
    cur.execute(sql)
    merged_rows = _rows_to_dicts(cur, cur.fetchmany(MAX_VIZ_ROWS))
    merged_columns = _col_meta(cur)

    # Build match pairs: list of {left_index, right_index} where keys match
    match_pairs: list[dict[str, int]] = []
    if left_key and right_key:
        for li, lr in enumerate(left_rows):
            lv = lr.get(left_key)
            for ri, rr in enumerate(right_rows):
                rv = rr.get(right_key)
                if lv is not None and rv is not None and str(lv) == str(rv):
                    match_pairs.append({"left_index": li, "right_index": ri})

    return {
        "viz_type": "join",
        "join_type": join_type,
        "left_table": left_table,
        "right_table": right_table,
        "left_alias": left_alias,
        "right_alias": right_alias,
        "left_columns": left_columns,
        "right_columns": right_columns,
        "left_rows": left_rows,
        "right_rows": right_rows,
        "on_condition": on_condition,
        "left_key": left_key,
        "right_key": right_key,
        "match_pairs": match_pairs,
        "merged_columns": merged_columns,
        "merged_rows": merged_rows,
    }


def _parse_join_info(sql: str) -> dict[str, str]:
    """
    Extract left table, right table, aliases, and ON condition from a single JOIN query.
    Handles: FROM t1 [AS] a JOIN t2 [AS] b ON a.col = b.col
    """
    result: dict[str, str] = {}

    # FROM clause  → left table + alias
    from_match = re.search(
        r"\bFROM\s+\[?(\w+)\]?(?:\s+(?:AS\s+)?(\w+))?",
        sql, re.IGNORECASE
    )
    if from_match:
        result["left_table"] = from_match.group(1)
        result["left_alias"] = from_match.group(2) or from_match.group(1)

    # JOIN clause  → right table + alias
    join_match = re.search(
        r"\bJOIN\s+\[?(\w+)\]?(?:\s+(?:AS\s+)?(\w+))?",
        sql, re.IGNORECASE
    )
    if join_match:
        result["right_table"] = join_match.group(1)
        result["right_alias"] = join_match.group(2) or join_match.group(1)

    # ON condition
    on_match = re.search(r"\bON\b(.+?)(?=\bWHERE\b|\bORDER\b|\bGROUP\b|\bLIMIT\b|$)",
                         sql, re.IGNORECASE | re.DOTALL)
    if on_match:
        on_text = on_match.group(1).strip()
        result["on_condition"] = on_text

        # Parse  alias.col = alias.col
        eq_match = re.search(
            r"(\w+)\.(\w+)\s*=\s*(\w+)\.(\w+)",
            on_text
        )
        if eq_match:
            la, lc, ra, rc = eq_match.groups()
            left_alias = result.get("left_alias", "")
            right_alias = result.get("right_alias", "")
            # Match aliases to determine which is left/right key
            if la.lower() == left_alias.lower():
                result["left_key"] = lc
                result["right_key"] = rc
            else:
                result["left_key"] = rc
                result["right_key"] = lc

    return result

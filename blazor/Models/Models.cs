namespace SqlVisualizer.Models;

// ── Connection ──────────────────────────────────────────────────────────────

public record ConnectionStatus(bool Connected, string? DisplayName = null);

// ── Schema ───────────────────────────────────────────────────────────────────

public record TableRef(string Schema, string Name);

public record ColumnMeta(
    string Name,
    string DataType,
    bool IsNullable,
    bool IsPrimaryKey,
    bool IsForeignKey,
    string? Default);

public record ForeignKey(string Column, string RefTable, string RefColumn);

public record ColumnsResponse(List<ColumnMeta> Columns, List<ForeignKey> ForeignKeys);

// ── Query results ─────────────────────────────────────────────────────────────

public record ColumnHeader(string Name);

/// Base for all visualisation result types.
public abstract record VizResult(string VizType);

public record PlainResult(
    string Type,                        // "SELECT" | "DML"
    List<ColumnHeader> Columns,
    List<Dictionary<string, object?>> Rows,
    int? RowsAffected,
    bool Truncated)
    : VizResult("plain");

/// A plain table scan — used to visualize simple queries (no WHERE/ORDER/JOIN).
public record ScanResult(
    List<ColumnHeader> Columns,
    List<Dictionary<string, object?>> Rows,
    int? Limit,
    bool Truncated)
    : VizResult("scan");

public record OrderByResult(
    List<ColumnHeader> Columns,
    List<Dictionary<string, object?>> UnsortedRows,
    List<Dictionary<string, object?>> SortedRows,
    List<int> SortKeyIndices,
    List<string> SortKeys,
    int? Limit)
    : VizResult("order_by");

public record WhereResult(
    List<ColumnHeader> Columns,
    List<Dictionary<string, object?>> AllRows,
    List<bool> MatchMask,
    string WhereText,
    List<string> Conditions,
    List<List<bool>> ConditionResults)
    : VizResult("where");

public record WhereOrderByResult(
    List<ColumnHeader> Columns,
    List<Dictionary<string, object?>> AllRows,
    List<bool> MatchMask,
    string WhereText,
    List<string> Conditions,
    List<List<bool>> ConditionResults,
    List<Dictionary<string, object?>> SortedRows,
    List<int> SortKeyIndices,
    List<string> SortKeys)
    : VizResult("where_order_by");

/// One JOIN in a chain, adding table[k+1]. Its ON condition references some
/// already-joined table[LeftTableIndex] (usually k, but earlier for star/snowflake
/// shapes). MatchPairs are (row in table[LeftTableIndex], row in table[k+1]).
public record JoinStep(
    string JoinType,
    string OnCondition,
    string? LeftKey,
    string? RightKey,
    List<(int L, int R)> MatchPairs,
    int LeftTableIndex);

/// A WHERE clause applied to the joined rows of a <see cref="JoinChainResult"/>.
/// Each list is indexed by joined row (parallel to JoinPaths / MergedRows).
public record JoinFilter(
    string WhereText,
    List<string> Conditions,                    // top-level AND parts of WhereText
    List<List<bool>> ConditionResults,          // [condition][joined row] → condition is TRUE
    List<bool> MatchMask,                       // joined row passes the whole WHERE
    List<ColumnHeader> ResultColumns);          // the SELECT projection (a subset of MergedColumns)

/// A chain of one or more joins across N tables laid out left-to-right.
public record JoinChainResult(
    List<string> TableNames,
    List<string?> Aliases,
    List<List<ColumnHeader>> TableColumns,
    List<List<Dictionary<string, object?>>> TableRows,
    List<JoinStep> Steps,                       // Count == TableNames.Count - 1
    List<int[]> JoinPaths,                      // one per joined row: source row index per table,
                                                // or NoMatch where an outer join filled in NULLs
    List<ColumnHeader> MergedColumns,           // projection, plus any columns the WHERE reads
    List<Dictionary<string, object?>> MergedRows,
    bool Truncated,
    JoinFilter? Filter = null)                  // null when the query has no WHERE
    : VizResult("join_chain")
{
    /// JoinPaths entry for a table that had no matching row (NULL-extended by an outer join).
    public const int NoMatch = -1;
}

// ── Scripts ───────────────────────────────────────────────────────────────────

public record Script(
    string Id,
    string Name,
    string EngineHint,
    string Content,
    DateTime CreatedAt,
    DateTime? LastUsed);

public record StatementResult(
    string Statement,
    string Type,            // "SELECT" | "DML" | "ERROR"
    int? RowsAffected,
    List<ColumnHeader>? Columns,
    List<Dictionary<string, object?>>? Rows,
    string? Error,
    bool Ok);

public record ScriptRunResult(string ScriptId, List<StatementResult> Results);

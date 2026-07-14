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

/// A chain of one or more joins across N tables laid out left-to-right.
public record JoinChainResult(
    List<string> TableNames,
    List<string?> Aliases,
    List<List<ColumnHeader>> TableColumns,
    List<List<Dictionary<string, object?>>> TableRows,
    List<JoinStep> Steps,                       // Count == TableNames.Count - 1
    List<int[]> JoinPaths,                       // one per output row: source row index per table
    List<ColumnHeader> MergedColumns,
    List<Dictionary<string, object?>> MergedRows,
    bool Truncated)
    : VizResult("join_chain");

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

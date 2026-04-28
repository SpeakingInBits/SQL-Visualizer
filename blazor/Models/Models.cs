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

public record OrderByResult(
    List<ColumnHeader> Columns,
    List<Dictionary<string, object?>> UnsortedRows,
    List<Dictionary<string, object?>> SortedRows,
    List<int> SortKeyIndices,
    List<string> SortKeys)
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

public record JoinResult(
    string JoinType,
    string LeftTable,
    string? LeftAlias,
    string RightTable,
    string? RightAlias,
    string OnCondition,
    string? LeftKey,
    string? RightKey,
    List<ColumnHeader> LeftColumns,
    List<ColumnHeader> RightColumns,
    List<Dictionary<string, object?>> LeftRows,
    List<Dictionary<string, object?>> RightRows,
    List<ColumnHeader> MergedColumns,
    List<Dictionary<string, object?>> MergedRows,
    List<(int L, int R)> MatchPairs)
    : VizResult("join");

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

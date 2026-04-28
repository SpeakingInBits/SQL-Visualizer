using Microsoft.Data.Sqlite;
using SqlVisualizer.Models;
using System.Text.RegularExpressions;

namespace SqlVisualizer.Services;

/// <summary>
/// Runs SQL statements and produces visualisation result objects.
/// Mirrors the logic in Python executor.py exactly, including all four viz types.
/// </summary>
public class QueryExecutorService
{
    private const int MaxVizRows = 200;

    private readonly SqliteConnectionService _conn;

    public QueryExecutorService(SqliteConnectionService conn) => _conn = conn;

    // ─────────────────────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────────────────────

    public PlainResult RunStatement(string sql)
    {
        var c = _conn.Require();
        return ExecuteStatement(c, sql);
    }

    public VizResult VisualizeQuery(string sql)
    {
        var c = _conn.Require();
        var upper = sql.ToUpperInvariant();

        bool hasJoin    = Regex.IsMatch(upper, @"\bJOIN\b");
        bool hasWhere   = Regex.IsMatch(upper, @"\bWHERE\b");
        bool hasOrderBy = Regex.IsMatch(upper, @"\bORDER\s+BY\b");

        if (hasJoin)    return VizJoin(c, sql);
        if (hasWhere && hasOrderBy) return VizWhereOrderBy(c, sql);
        if (hasOrderBy) return VizOrderBy(c, sql);
        if (hasWhere)   return VizWhere(c, sql);

        var plain = ExecuteStatement(c, sql);
        return plain;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Statement execution helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static PlainResult ExecuteStatement(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;

        // Detect statement type from first non-whitespace keyword
        var upper = sql.TrimStart().ToUpperInvariant();
        bool isSelect = upper.StartsWith("SELECT") || upper.StartsWith("WITH");

        if (isSelect)
        {
            using var reader = cmd.ExecuteReader();
            var cols = GetColumns(reader);
            var rows = new List<Dictionary<string, object?>>();
            bool truncated = false;

            while (reader.Read())
            {
                if (rows.Count >= MaxVizRows) { truncated = true; break; }
                rows.Add(ReadRow(reader, cols));
            }

            return new PlainResult("SELECT", cols, rows, null, truncated);
        }
        else
        {
            // DML — need a transaction
            using var tx = c.BeginTransaction();
            cmd.Transaction = tx;
            int affected = cmd.ExecuteNonQuery();
            tx.Commit();
            return new PlainResult("DML", [], [], affected, false);
        }
    }

    private static List<Dictionary<string, object?>> FetchRows(
        SqliteConnection c, string sql, int limit = MaxVizRows)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        var cols = GetColumns(reader);
        var rows = new List<Dictionary<string, object?>>();
        while (reader.Read())
        {
            if (rows.Count >= limit) break;
            rows.Add(ReadRow(reader, cols));
        }
        return rows;
    }

    private static (List<ColumnHeader> Cols, List<Dictionary<string, object?>> Rows)
        FetchWithColumns(SqliteConnection c, string sql, int limit = MaxVizRows)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        var cols = GetColumns(reader);
        var rows = new List<Dictionary<string, object?>>();
        while (reader.Read())
        {
            if (rows.Count >= limit) break;
            rows.Add(ReadRow(reader, cols));
        }
        return (cols, rows);
    }

    private static List<ColumnHeader> GetColumns(SqliteDataReader reader)
    {
        var cols = new List<ColumnHeader>(reader.FieldCount);
        for (int i = 0; i < reader.FieldCount; i++)
            cols.Add(new ColumnHeader(reader.GetName(i)));
        return cols;
    }

    private static Dictionary<string, object?> ReadRow(
        SqliteDataReader reader, List<ColumnHeader> cols)
    {
        var row = new Dictionary<string, object?>(cols.Count);
        for (int i = 0; i < cols.Count; i++)
            row[cols[i].Name] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        return row;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  ORDER BY visualiser
    // ─────────────────────────────────────────────────────────────────────────

    private static OrderByResult VizOrderBy(SqliteConnection c, string sql)
    {
        // Extract ORDER BY clause and sort keys
        var orderByMatch = Regex.Match(sql, @"\bORDER\s+BY\b(.+?)(?:LIMIT|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var orderByClause = orderByMatch.Success ? orderByMatch.Groups[1].Value.Trim() : "";

        // Parse sort key column names
        var sortKeys = orderByClause
            .Split(',')
            .Select(k => Regex.Match(k.Trim(), @"^(\w+)").Groups[1].Value)
            .Where(k => k.Length > 0)
            .ToList();

        // Unsorted: strip ORDER BY
        var unsortedSql = Regex.Replace(sql, @"\bORDER\s+BY\b.+", "",
            RegexOptions.IgnoreCase | RegexOptions.Singleline).Trim();
        var (cols, unsortedRows) = FetchWithColumns(c, unsortedSql);

        var (_, sortedRows) = FetchWithColumns(c, sql);

        // Find column indices for the sort keys
        var sortKeyIndices = sortKeys
            .Select(k => cols.FindIndex(col =>
                string.Equals(col.Name, k, StringComparison.OrdinalIgnoreCase)))
            .Where(i => i >= 0)
            .ToList();

        return new OrderByResult(cols, unsortedRows, sortedRows, sortKeyIndices, sortKeys);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  WHERE visualiser
    // ─────────────────────────────────────────────────────────────────────────

    private static WhereResult VizWhere(SqliteConnection c, string sql)
    {
        // Extract WHERE text
        var whereMatch = Regex.Match(sql,
            @"\bWHERE\b\s*(.+?)(?:\bORDER\s+BY\b|\bGROUP\s+BY\b|\bHAVING\b|\bLIMIT\b|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var whereText = whereMatch.Success ? whereMatch.Groups[1].Value.Trim() : "";

        // All rows (no WHERE)
        var noWhereSql = Regex.Replace(
            Regex.Replace(sql, @"\bWHERE\b.+?(?=\bORDER\s+BY\b|\bGROUP\s+BY\b|\bLIMIT\b|$)",
                " ", RegexOptions.IgnoreCase | RegexOptions.Singleline),
            @"\s+", " ").Trim();
        // Also strip ORDER BY from allRows query
        noWhereSql = Regex.Replace(noWhereSql, @"\bORDER\s+BY\b.+", "",
            RegexOptions.IgnoreCase | RegexOptions.Singleline).Trim();

        var (cols, allRows) = FetchWithColumns(c, noWhereSql);

        // Split top-level AND conditions
        var conditions = SplitTopLevelAnd(whereText);

        // Build per-row condition matrix
        // Base of the query (up to WHERE keyword)
        var baseMatch = Regex.Match(sql,
            @"^(.+?)\bWHERE\b", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var baseQuery = baseMatch.Success ? baseMatch.Groups[1].Value.Trim() : $"SELECT * FROM ({sql})";
        // Strip any ORDER BY from base
        baseQuery = Regex.Replace(baseQuery, @"\bORDER\s+BY\b.+", "",
            RegexOptions.IgnoreCase | RegexOptions.Singleline).Trim();

        // Determine primary key or rowid to correlate rows
        // We'll run each condition separately and rely on row order (same base query = same order)
        var conditionResults = new List<List<bool>>();
        foreach (var cond in conditions)
        {
            var condSql = $"{baseQuery} WHERE {cond}";
            var condRows = FetchRows(c, condSql);
            // Build a set of row representations to test membership
            var condSet = condRows.Select(r => RowKey(r)).ToHashSet();

            var colResults = allRows.Select(r => condSet.Contains(RowKey(r))).ToList();
            conditionResults.Add(colResults);
        }

        // Match mask = AND of all conditions
        var matchMask = Enumerable.Range(0, allRows.Count)
            .Select(i => conditionResults.All(cr => i < cr.Count && cr[i]))
            .ToList();

        return new WhereResult(cols, allRows, matchMask, whereText, conditions, conditionResults);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  WHERE + ORDER BY visualiser
    // ─────────────────────────────────────────────────────────────────────────

    private static WhereOrderByResult VizWhereOrderBy(SqliteConnection c, string sql)
    {
        var whereResult = VizWhere(c, sql);

        // Run full original query (with WHERE + ORDER BY) to get sorted filtered rows
        var orderByMatch = Regex.Match(sql, @"\bORDER\s+BY\b(.+?)(?:LIMIT|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var orderByClause = orderByMatch.Success ? orderByMatch.Groups[1].Value.Trim() : "";
        var sortKeys = orderByClause
            .Split(',')
            .Select(k => Regex.Match(k.Trim(), @"^(\w+)").Groups[1].Value)
            .Where(k => k.Length > 0)
            .ToList();

        var (_, sortedRows) = FetchWithColumns(c, sql);

        var sortKeyIndices = sortKeys
            .Select(k => whereResult.Columns.FindIndex(col =>
                string.Equals(col.Name, k, StringComparison.OrdinalIgnoreCase)))
            .Where(i => i >= 0)
            .ToList();

        return new WhereOrderByResult(
            whereResult.Columns,
            whereResult.AllRows,
            whereResult.MatchMask,
            whereResult.WhereText,
            whereResult.Conditions,
            whereResult.ConditionResults,
            sortedRows,
            sortKeyIndices,
            sortKeys);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  JOIN visualiser
    // ─────────────────────────────────────────────────────────────────────────

    private const int MaxJoinRows = 10;

    private static JoinResult VizJoin(SqliteConnection c, string sql)
    {
        // Extract JOIN type and table names
        var joinMatch = Regex.Match(sql,
            @"\bFROM\s+(\w+)(?:\s+(?:AS\s+)?(\w+))?\s+((?:INNER\s+|LEFT\s+(?:OUTER\s+)?|RIGHT\s+(?:OUTER\s+)?|FULL\s+(?:OUTER\s+)?|CROSS\s+)?JOIN)\s+(\w+)(?:\s+(?:AS\s+)?(\w+))?\s+(?:ON\s+(.+?))?(?:\s*WHERE|\s*ORDER|\s*GROUP|\s*LIMIT|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        string leftTable  = joinMatch.Success ? joinMatch.Groups[1].Value : "";
        string? leftAlias = joinMatch.Success && joinMatch.Groups[2].Length > 0 ? joinMatch.Groups[2].Value : null;
        string joinType   = joinMatch.Success ? joinMatch.Groups[3].Value.Trim().ToUpperInvariant() : "JOIN";
        string rightTable = joinMatch.Success ? joinMatch.Groups[4].Value : "";
        string? rightAlias = joinMatch.Success && joinMatch.Groups[5].Length > 0 ? joinMatch.Groups[5].Value : null;
        string onCondition = joinMatch.Success && joinMatch.Groups[6].Length > 0 ? joinMatch.Groups[6].Value.Trim() : "";

        // Parse key columns from ON clause  (e.g. a.id = b.a_id)
        string? leftKey = null, rightKey = null;
        var onMatch = Regex.Match(onCondition, @"(\w+)\.(\w+)\s*=\s*(\w+)\.(\w+)");
        if (onMatch.Success)
        {
            leftKey  = onMatch.Groups[2].Value;
            rightKey = onMatch.Groups[4].Value;
        }

        // Fetch left and right table rows independently (cap to MaxJoinRows each)
        var leftRef  = leftAlias  ?? leftTable;
        var rightRef = rightAlias ?? rightTable;

        var (leftCols, leftRows)   = FetchWithColumns(c, $"SELECT * FROM {leftTable}", MaxJoinRows);
        var (rightCols, rightRows) = FetchWithColumns(c, $"SELECT * FROM {rightTable}", MaxJoinRows);

        // Fetch merged (joined) rows
        var (mergedCols, mergedRows) = FetchWithColumns(c, sql);

        // Build match pairs: for each merged row find matching left/right row indices
        var matchPairs = new List<(int L, int R)>();
        foreach (var mr in mergedRows)
        {
            for (int li = 0; li < leftRows.Count; li++)
            {
                for (int ri = 0; ri < rightRows.Count; ri++)
                {
                    if (RowsMatchJoin(mr, leftRows[li], leftCols, leftAlias) &&
                        RowsMatchJoin(mr, rightRows[ri], rightCols, rightAlias))
                    {
                        var pair = (li, ri);
                        if (!matchPairs.Contains(pair))
                            matchPairs.Add(pair);
                    }
                }
            }
        }

        return new JoinResult(
            JoinType:      joinType,
            LeftTable:     leftTable,
            LeftAlias:     leftAlias,
            RightTable:    rightTable,
            RightAlias:    rightAlias,
            OnCondition:   onCondition,
            LeftKey:       leftKey,
            RightKey:      rightKey,
            LeftColumns:   leftCols,
            RightColumns:  rightCols,
            LeftRows:      leftRows,
            RightRows:     rightRows,
            MergedColumns: mergedCols,
            MergedRows:    mergedRows,
            MatchPairs:    matchPairs);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Splits a WHERE expression on top-level AND (respects parentheses).</summary>
    private static List<string> SplitTopLevelAnd(string expr)
    {
        var parts = new List<string>();
        int depth = 0;
        int start = 0;
        for (int i = 0; i < expr.Length; i++)
        {
            if (expr[i] == '(') depth++;
            else if (expr[i] == ')') depth--;
            else if (depth == 0 && i + 3 < expr.Length &&
                     expr[i..].StartsWith("AND", StringComparison.OrdinalIgnoreCase) &&
                     (i == 0 || !char.IsLetterOrDigit(expr[i - 1])) &&
                     !char.IsLetterOrDigit(expr[i + 3]))
            {
                parts.Add(expr[start..i].Trim());
                start = i + 3;
                i += 2;
            }
        }
        parts.Add(expr[start..].Trim());
        return parts.Where(p => p.Length > 0).ToList();
    }

    /// <summary>Generates a stable string key for a row (for set membership tests).</summary>
    private static string RowKey(Dictionary<string, object?> row)
        => string.Join("|", row.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"));

    /// <summary>
    /// Checks if a merged join row contains all values from a side-table row.
    /// Handles both plain and alias-prefixed column names.
    /// </summary>
    private static bool RowsMatchJoin(
        Dictionary<string, object?> merged,
        Dictionary<string, object?> sideRow,
        List<ColumnHeader> sideCols,
        string? alias)
    {
        foreach (var col in sideCols)
        {
            var colName = col.Name;
            // Try plain name first, then alias.colname
            object? mergedVal = null;
            bool found = merged.TryGetValue(colName, out mergedVal) ||
                         (alias != null && merged.TryGetValue($"{alias}.{colName}", out mergedVal));
            if (!found) return false;
            var sideVal = sideRow.TryGetValue(colName, out var sv) ? sv : null;
            if (!Equals(mergedVal, sideVal)) return false;
        }
        return true;
    }
}

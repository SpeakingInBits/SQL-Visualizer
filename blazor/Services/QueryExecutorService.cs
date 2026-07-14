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

        if (hasJoin)    return VizJoinChain(c, sql);
        if (hasWhere && hasOrderBy) return VizWhereOrderBy(c, sql);
        if (hasOrderBy) return VizOrderBy(c, sql);
        if (hasWhere)   return VizWhere(c, sql);

        // Simple query (no WHERE/ORDER/JOIN) → row-by-row table scan
        var trimmed = sql.TrimStart().ToUpperInvariant();
        if (trimmed.StartsWith("SELECT") || trimmed.StartsWith("WITH"))
            return VizScan(c, sql);

        return ExecuteStatement(c, sql);
    }

    /// <summary>Extracts a trailing LIMIT value, if present.</summary>
    private static int? ParseLimit(string sql)
    {
        var m = Regex.Match(sql, @"\bLIMIT\s+(\d+)", RegexOptions.IgnoreCase);
        return m.Success && int.TryParse(m.Groups[1].Value, out var n) ? n : null;
    }

    /// <summary>Removes a trailing LIMIT clause so the pre-limit rows can be shown.</summary>
    private static string StripLimit(string sql)
        => Regex.Replace(sql, @"\bLIMIT\s+\d+(\s+OFFSET\s+\d+)?", "",
            RegexOptions.IgnoreCase | RegexOptions.Singleline).Trim().TrimEnd(';').Trim();

    // ─────────────────────────────────────────────────────────────────────────
    //  Scan visualiser (simple queries)
    // ─────────────────────────────────────────────────────────────────────────

    private static ScanResult VizScan(SqliteConnection c, string sql)
    {
        var limit = ParseLimit(sql);
        // Fetch without LIMIT so the animation can show which rows get cut
        var baseSql = StripLimit(sql);
        using var cmd = c.CreateCommand();
        cmd.CommandText = baseSql;
        using var reader = cmd.ExecuteReader();
        var cols = GetColumns(reader);
        var rows = new List<Dictionary<string, object?>>();
        bool truncated = false;
        while (reader.Read())
        {
            if (rows.Count >= MaxVizRows) { truncated = true; break; }
            rows.Add(ReadRow(reader, cols));
        }
        return new ScanResult(cols, rows, limit, truncated);
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

        // Unsorted: strip ORDER BY (and any LIMIT)
        var unsortedSql = StripLimit(Regex.Replace(sql, @"\bORDER\s+BY\b.+", "",
            RegexOptions.IgnoreCase | RegexOptions.Singleline).Trim());
        var (cols, unsortedRows) = FetchWithColumns(c, unsortedSql);

        // Sorted, but without LIMIT so the animation can show the cut
        var limit = ParseLimit(sql);
        var (_, sortedRows) = FetchWithColumns(c, StripLimit(sql));

        // Find column indices for the sort keys
        var sortKeyIndices = sortKeys
            .Select(k => cols.FindIndex(col =>
                string.Equals(col.Name, k, StringComparison.OrdinalIgnoreCase)))
            .Where(i => i >= 0)
            .ToList();

        return new OrderByResult(cols, unsortedRows, sortedRows, sortKeyIndices, sortKeys, limit);
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
        // Drop a trailing statement terminator so it doesn't leak into the last condition
        whereText = whereText.TrimEnd().TrimEnd(';').TrimEnd();

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

    private const int MaxJoinRows = 50;

    // A single JOIN clause: optional type, table, optional alias, optional ON condition.
    private const string JoinPattern =
        @"(INNER\s+|LEFT\s+(?:OUTER\s+)?|RIGHT\s+(?:OUTER\s+)?|FULL\s+(?:OUTER\s+)?|CROSS\s+)?" +
        @"JOIN\s+(\w+)(?:\s+(?:AS\s+)?(\w+))?(?:\s+ON\s+(.+?))?" +
        @"(?=\s*(?:INNER\s+|LEFT\s+(?:OUTER\s+)?|RIGHT\s+(?:OUTER\s+)?|FULL\s+(?:OUTER\s+)?|CROSS\s+)?JOIN\b|$)";

    private static readonly HashSet<string> SqlKeywords = new(StringComparer.OrdinalIgnoreCase)
        { "JOIN", "INNER", "LEFT", "RIGHT", "FULL", "OUTER", "CROSS", "ON", "WHERE", "ORDER", "GROUP", "LIMIT", "AS" };

    private static string? AliasOrNull(string s)
        => string.IsNullOrEmpty(s) || SqlKeywords.Contains(s) ? null : s;

    private static JoinChainResult VizJoinChain(SqliteConnection c, string sql)
    {
        // Isolate the FROM…JOIN chain (up to WHERE/GROUP/ORDER/LIMIT)
        var fromMatch = Regex.Match(sql,
            @"\bFROM\s+(.+?)(?:\bWHERE\b|\bGROUP\s+BY\b|\bORDER\s+BY\b|\bLIMIT\b|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var fromClause = fromMatch.Success ? fromMatch.Groups[1].Value.Trim() : "";

        // First (driving) table + optional alias
        var firstMatch = Regex.Match(fromClause, @"^\s*(\w+)(?:\s+(?:AS\s+)?(\w+))?", RegexOptions.IgnoreCase);
        if (!firstMatch.Success || firstMatch.Groups[1].Length == 0)
            throw new InvalidOperationException(
                "Could not parse the FROM clause for visualization.");

        var tableNames = new List<string> { firstMatch.Groups[1].Value };
        var aliases    = new List<string?> { AliasOrNull(firstMatch.Groups[2].Value) };
        var onConds    = new List<string>();
        var joinTypes  = new List<string>();

        foreach (Match jm in Regex.Matches(fromClause, JoinPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            joinTypes.Add(jm.Groups[1].Length > 0 ? jm.Groups[1].Value.Trim().ToUpperInvariant() + " JOIN" : "JOIN");
            tableNames.Add(jm.Groups[2].Value);
            aliases.Add(AliasOrNull(jm.Groups[3].Value));
            onConds.Add(jm.Groups[4].Value.Trim());
        }

        if (tableNames.Count < 2)
            throw new InvalidOperationException(
                "Could not parse this JOIN for visualization. Supported form: " +
                "FROM a [x] JOIN b [y] ON x.col = y.col [JOIN c …]");

        // Fetch each participating table (capped)
        var tableCols = new List<List<ColumnHeader>>();
        var tableRows = new List<List<Dictionary<string, object?>>>();
        bool truncated = false;
        foreach (var t in tableNames)
        {
            var (cols, rows) = FetchWithColumns(c, $"SELECT * FROM {t}", MaxJoinRows);
            tableCols.Add(cols);
            tableRows.Add(rows);
            if (rows.Count >= MaxJoinRows) truncated = true;
        }

        // Map each table name / alias → its index, so an ON condition can be tied
        // back to whichever already-joined table it references (not necessarily the
        // immediately-preceding one, as in star/snowflake shapes).
        var refToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int k = 0; k < tableNames.Count; k++)
        {
            refToIdx[tableNames[k]] = k;
            if (aliases[k] != null) refToIdx[aliases[k]!] = k;
        }

        // Build one JoinStep per join, adding table[k+1] and matching its rows to the
        // rows of whichever prior table the ON condition's left side references.
        var steps = new List<JoinStep>();
        for (int k = 0; k < onConds.Count; k++)
        {
            var (leftRef, leftKey, rightKey) = ParseJoinKeys(onConds[k], aliases[k + 1] ?? tableNames[k + 1]);
            // Resolve the prior table the left key lives on; fall back to table[k]
            // when the reference can't be resolved to an already-joined table.
            int leftIdx = k;
            if (leftRef != null && refToIdx.TryGetValue(leftRef, out var ri0) && ri0 <= k) leftIdx = ri0;

            var pairs = new List<(int L, int R)>();
            if (leftKey != null && rightKey != null)
            {
                var left = tableRows[leftIdx];
                var right = tableRows[k + 1];
                for (int li = 0; li < left.Count; li++)
                {
                    if (!left[li].TryGetValue(leftKey, out var lv)) continue;
                    for (int ri = 0; ri < right.Count; ri++)
                    {
                        if (right[ri].TryGetValue(rightKey, out var rv) && Equals(lv?.ToString(), rv?.ToString()))
                            pairs.Add((li, ri));
                    }
                }
            }
            steps.Add(new JoinStep(joinTypes[k], onConds[k], leftKey, rightKey, pairs, leftIdx));
        }

        // Enumerate inner-join paths (nested loop, first table outer) so each
        // output row maps to the exact source rows across every table.
        var paths = EnumerateJoinPaths(steps, tableRows[0].Count, MaxJoinRows);
        if (paths.Count >= MaxJoinRows) truncated = true;

        // Build the joined result rows from the SELECT projection
        var specs = ParseProjection(sql, tableNames, aliases, tableCols);
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var keys = new List<string>();
        var mergedCols = new List<ColumnHeader>();
        foreach (var s in specs)
        {
            var name = s.Header;
            if (seen.TryGetValue(name, out var cnt)) { seen[name] = cnt + 1; name = $"{name}_{cnt + 1}"; }
            else seen[name] = 1;
            keys.Add(name);
            mergedCols.Add(new ColumnHeader(name));
        }
        var mergedRows = paths.Select(p =>
        {
            var row = new Dictionary<string, object?>();
            for (int s = 0; s < specs.Count; s++)
            {
                var src = tableRows[specs[s].T][p[specs[s].T]];
                row[keys[s]] = src.TryGetValue(specs[s].Col, out var v) ? v : null;
            }
            return row;
        }).ToList();

        return new JoinChainResult(tableNames, aliases, tableCols, tableRows, steps,
            paths, mergedCols, mergedRows, truncated);
    }

    /// <summary>Enumerates inner-join row-index tuples across the table chain.</summary>
    private static List<int[]> EnumerateJoinPaths(List<JoinStep> steps, int firstCount, int cap)
    {
        var partial = new List<List<int>>();
        for (int i = 0; i < firstCount; i++) partial.Add(new List<int> { i });

        foreach (var step in steps)
        {
            var adj = new Dictionary<int, List<int>>();
            foreach (var (L, R) in step.MatchPairs)
            {
                if (!adj.TryGetValue(L, out var list)) { list = new(); adj[L] = list; }
                list.Add(R);
            }
            var next = new List<List<int>>();
            foreach (var p in partial)
            {
                if (adj.TryGetValue(p[step.LeftTableIndex], out var rs))
                    foreach (var r in rs)
                    {
                        next.Add(new List<int>(p) { r });
                        if (next.Count >= cap) break;
                    }
                if (next.Count >= cap) break;
            }
            partial = next;
        }
        return partial.Take(cap).Select(p => p.ToArray()).ToList();
    }

    /// <summary>Resolves the SELECT list into (header, tableIndex, column) specs.
    /// Falls back to all columns (alias-qualified) for `*` or unsupported expressions.</summary>
    private static List<(string Header, int T, string Col)> ParseProjection(
        string sql, List<string> tableNames, List<string?> aliases, List<List<ColumnHeader>> tableCols)
    {
        var refToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int k = 0; k < tableNames.Count; k++)
        {
            refToIdx[tableNames[k]] = k;
            if (aliases[k] != null) refToIdx[aliases[k]!] = k;
        }

        string RefLabel(int k) => aliases[k] ?? tableNames[k];
        void AddTable(List<(string, int, string)> acc, int k)
        {
            foreach (var col in tableCols[k]) acc.Add(($"{RefLabel(k)}.{col.Name}", k, col.Name));
        }
        List<(string, int, string)> AllColumns()
        {
            var acc = new List<(string, int, string)>();
            for (int k = 0; k < tableNames.Count; k++) AddTable(acc, k);
            return acc;
        }

        var selMatch = Regex.Match(sql, @"\bSELECT\b\s+(?:DISTINCT\s+)?(.+?)\s+\bFROM\b",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!selMatch.Success) return AllColumns();

        var specs = new List<(string, int, string)>();
        foreach (var raw in SplitTopLevelComma(selMatch.Groups[1].Value))
        {
            var item = raw.Trim();
            string? outName = null;
            var asm = Regex.Match(item, @"^(.*?)\s+AS\s+(\w+)$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (asm.Success) { item = asm.Groups[1].Value.Trim(); outName = asm.Groups[2].Value; }

            if (item == "*") { specs.AddRange(AllColumns()); continue; }

            var starM = Regex.Match(item, @"^(\w+)\.\*$");
            if (starM.Success)
            {
                if (refToIdx.TryGetValue(starM.Groups[1].Value, out var ti)) AddTable(specs, ti);
                else return AllColumns();
                continue;
            }
            var qualM = Regex.Match(item, @"^(\w+)\.(\w+)$");
            if (qualM.Success)
            {
                if (refToIdx.TryGetValue(qualM.Groups[1].Value, out var ti))
                    specs.Add((outName ?? qualM.Groups[2].Value, ti, qualM.Groups[2].Value));
                else return AllColumns();
                continue;
            }
            var bareM = Regex.Match(item, @"^(\w+)$");
            if (bareM.Success)
            {
                int ti = FindTableWithColumn(tableCols, bareM.Groups[1].Value);
                if (ti >= 0) specs.Add((outName ?? bareM.Groups[1].Value, ti, bareM.Groups[1].Value));
                else return AllColumns();
                continue;
            }
            // Unsupported expression (function, arithmetic, …) → show every column instead
            return AllColumns();
        }
        return specs.Count > 0 ? specs : AllColumns();
    }

    private static int FindTableWithColumn(List<List<ColumnHeader>> tableCols, string col)
    {
        for (int k = 0; k < tableCols.Count; k++)
            if (tableCols[k].Any(c => string.Equals(c.Name, col, StringComparison.OrdinalIgnoreCase)))
                return k;
        return -1;
    }

    /// <summary>Splits a comma-separated list, respecting parentheses depth.</summary>
    private static List<string> SplitTopLevelComma(string expr)
    {
        var parts = new List<string>();
        int depth = 0, start = 0;
        for (int i = 0; i < expr.Length; i++)
        {
            char ch = expr[i];
            if (ch == '(') depth++;
            else if (ch == ')') depth--;
            else if (ch == ',' && depth == 0) { parts.Add(expr[start..i]); start = i + 1; }
        }
        parts.Add(expr[start..]);
        return parts.Where(p => p.Trim().Length > 0).ToList();
    }

    /// <summary>Parses an ON condition, returning the ref (table name/alias) and column
    /// on the prior-table side, plus the column on the newly-joined table.</summary>
    private static (string? LeftRef, string? LeftKey, string? RightKey) ParseJoinKeys(
        string onCondition, string newTableRef)
    {
        var m = Regex.Match(onCondition, @"(\w+)\.(\w+)\s*=\s*(\w+)\.(\w+)");
        if (!m.Success) return (null, null, null);
        string aRef = m.Groups[1].Value, aCol = m.Groups[2].Value;
        string bRef = m.Groups[3].Value, bCol = m.Groups[4].Value;
        // The side referencing the newly-joined table is the "right" key; the other
        // side is the "left" key and its ref tells us which prior table it lives on.
        if (string.Equals(bRef, newTableRef, StringComparison.OrdinalIgnoreCase)) return (aRef, aCol, bCol);
        if (string.Equals(aRef, newTableRef, StringComparison.OrdinalIgnoreCase)) return (bRef, bCol, aCol);
        return (aRef, aCol, bCol);
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
}

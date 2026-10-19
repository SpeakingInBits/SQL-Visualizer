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

    /// <summary>True when a query has a structure the visualizer can animate — i.e. it
    /// reads rows (SELECT / WITH). DML and other statements have nothing to show.</summary>
    public static bool IsVisualizable(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return false;
        var t = sql.TrimStart().ToUpperInvariant();
        return t.StartsWith("SELECT") || t.StartsWith("WITH");
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

    /// <summary>The WHERE clause body (without the keyword or a trailing ';'), or "" if none.</summary>
    private static string ExtractWhere(string sql)
    {
        var whereMatch = Regex.Match(sql,
            @"\bWHERE\b\s*(.+?)(?:\bORDER\s+BY\b|\bGROUP\s+BY\b|\bHAVING\b|\bLIMIT\b|\bUNION\b|\bINTERSECT\b|\bEXCEPT\b|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var whereText = whereMatch.Success ? whereMatch.Groups[1].Value.Trim() : "";
        // Drop a trailing statement terminator so it doesn't leak into the last condition
        return whereText.TrimEnd().TrimEnd(';').TrimEnd();
    }

    private static WhereResult VizWhere(SqliteConnection c, string sql)
    {
        var whereText = ExtractWhere(sql);

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
        // In a compound query (… UNION SELECT …) only the first SELECT is visualized
        sql = Regex.Split(sql, @"\b(?:UNION|INTERSECT|EXCEPT)\b", RegexOptions.IgnoreCase)[0];

        // Isolate the FROM…JOIN chain (up to WHERE/GROUP/ORDER/LIMIT)
        var fromMatch = Regex.Match(sql,
            @"\bFROM\s+(.+?)(?:\bWHERE\b|\bGROUP\s+BY\b|\bORDER\s+BY\b|\bLIMIT\b|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var fromClause = fromMatch.Success ? fromMatch.Groups[1].Value.Trim().TrimEnd(';').TrimEnd() : "";

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
            joinTypes.Add(jm.Groups[1].Length > 0
                ? Regex.Replace(jm.Groups[1].Value.Trim().ToUpperInvariant(), @"\s+", " ") + " JOIN"
                : "JOIN");
            tableNames.Add(jm.Groups[2].Value);
            aliases.Add(AliasOrNull(jm.Groups[3].Value));
            onConds.Add(jm.Groups[4].Value.Trim());
        }

        if (tableNames.Count < 2)
            throw new InvalidOperationException(
                "Could not parse this JOIN for visualization. Supported form: " +
                "FROM a [x] JOIN b [y] ON x.col = y.col [JOIN c …]");

        var whereText  = ExtractWhere(sql);
        var conditions = whereText.Length > 0 ? SplitTopLevelAnd(whereText) : new List<string>();

        // Preferred: let SQLite perform the join and report which source rows formed each
        // joined row. Falls back to matching equi-join keys here when the sources have no
        // rowid (views, WITHOUT ROWID tables) — inner-join semantics only, WHERE not applied.
        var exact = TryJoinViaRowids(c, tableNames, aliases, fromClause, whereText, conditions);

        List<List<ColumnHeader>> tableCols;
        List<List<Dictionary<string, object?>>> tableRows;
        bool truncated;
        if (exact != null)
        {
            tableCols = exact.TableCols;
            tableRows = exact.TableRows;
            truncated = exact.Truncated;
        }
        else
        {
            tableCols = new();
            tableRows = new();
            truncated = false;
            foreach (var t in tableNames)
            {
                var (cols, rows) = FetchWithColumns(c, $"SELECT * FROM {t}", MaxJoinRows);
                tableCols.Add(cols);
                tableRows.Add(rows);
                if (rows.Count >= MaxJoinRows) truncated = true;
            }
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
        // rows of whichever prior table the ON condition references.
        var steps = new List<JoinStep>();
        for (int k = 0; k < onConds.Count; k++)
        {
            var (leftIdx, leftKey, rightKey) =
                ResolveJoinKeys(onConds[k], k + 1, tableCols, refToIdx);

            var pairs = new List<(int L, int R)>();
            if (exact != null)
            {
                pairs = exact.Paths
                    .Where(p => p[leftIdx] != JoinChainResult.NoMatch && p[k + 1] != JoinChainResult.NoMatch)
                    .Select(p => (p[leftIdx], p[k + 1]))
                    .Distinct().ToList();
            }
            else if (leftKey != null && rightKey != null)
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

        List<int[]> paths;
        if (exact != null) paths = exact.Paths;
        else
        {
            // Enumerate inner-join paths (nested loop, first table outer) so each
            // output row maps to the exact source rows across every table.
            paths = EnumerateJoinPaths(steps, tableRows[0].Count, MaxJoinRows);
            if (paths.Count >= MaxJoinRows) truncated = true;
        }

        // Joined-row columns: the SELECT projection, plus (when filtering) any column the
        // WHERE reads that the projection leaves out — so you can see why a row passes.
        var specs = ParseProjection(sql, tableNames, aliases, tableCols);
        var joinedSpecs = new List<(string Header, int T, string Col)>(specs);
        bool filtering = exact != null && whereText.Length > 0;
        if (filtering)
            foreach (var extra in ReferencedColumns(whereText, tableNames, aliases, tableCols, refToIdx))
                if (!joinedSpecs.Any(s => s.T == extra.T && string.Equals(s.Col, extra.Col, StringComparison.OrdinalIgnoreCase)))
                    joinedSpecs.Add(extra);

        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var keys = new List<string>();
        var mergedCols = new List<ColumnHeader>();
        foreach (var s in joinedSpecs)
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
            for (int s = 0; s < joinedSpecs.Count; s++)
            {
                // A table the outer join found no match in contributes NULLs
                int ri = p[joinedSpecs[s].T];
                row[keys[s]] = ri != JoinChainResult.NoMatch
                    && tableRows[joinedSpecs[s].T][ri].TryGetValue(joinedSpecs[s].Col, out var v) ? v : null;
            }
            return row;
        }).ToList();

        JoinFilter? filter = filtering
            ? new JoinFilter(whereText, conditions, exact!.ConditionResults, exact.MatchMask,
                             mergedCols.Take(specs.Count).ToList())
            : null;

        // In-depth mode: every left/right row comparison, including the ones that fail
        bool[][]? comparisons = null;
        string? unavailable;
        long pairCount = tableRows.Count == 2 ? (long)tableRows[0].Count * tableRows[1].Count : 0;
        if (steps.Count != 1)
            unavailable = "In-depth view is available for queries with a single JOIN";
        else if (onConds[0].Length == 0)
            unavailable = "A join without an ON condition has no comparisons to show";
        else if (exact == null)
            unavailable = "In-depth view needs tables with rowids (not views)";
        else if (truncated || pairCount > MaxComparisonPairs)
            unavailable = $"Too many rows for the in-depth view (at most {MaxComparisonPairs} row pairs)";
        else
        {
            comparisons = CompareAllPairs(c, tableNames, aliases, onConds[0], exact.RowIds);
            unavailable = null;
        }

        return new JoinChainResult(tableNames, aliases, tableCols, tableRows, steps,
            paths, mergedCols, mergedRows, truncated, filter, comparisons, unavailable);
    }

    /// <summary>The most left×right row pairs the in-depth join view will compare.</summary>
    private const int MaxComparisonPairs = 625;

    /// <summary>Has SQLite evaluate a single join's ON condition for every pair of source rows —
    /// the nested loop a join conceptually runs — so the in-depth view can show the pairs that
    /// fail as well as the ones that match. NULL (unknown) counts as false, as it does in ON.</summary>
    private static bool[][] CompareAllPairs(SqliteConnection c, List<string> tableNames,
        List<string?> aliases, string onCond, List<List<long>> rowIds)
    {
        string Source(int k) => aliases[k] != null ? $"{tableNames[k]} {aliases[k]}" : tableNames[k];
        string Ref(int k) => aliases[k] ?? tableNames[k];

        var posOf = rowIds.Select(ids => ids.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i)).ToList();
        var result = rowIds[0].Select(_ => new bool[rowIds[1].Count]).ToArray();

        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT {Ref(0)}.rowid, {Ref(1)}.rowid, CASE WHEN ({onCond}) THEN 1 ELSE 0 END " +
                          $"FROM {Source(0)} CROSS JOIN {Source(1)}";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (posOf[0].TryGetValue(reader.GetInt64(0), out var l) && posOf[1].TryGetValue(reader.GetInt64(1), out var r))
                result[l][r] = reader.GetInt64(2) != 0;
        }
        return result;
    }

    private sealed record RowidJoin(
        List<List<ColumnHeader>> TableCols,
        List<List<Dictionary<string, object?>>> TableRows,
        List<List<long>> RowIds,                 // [table][row position] → rowid
        List<int[]> Paths,                       // sorted by source row, NoMatch last
        List<List<bool>> ConditionResults,       // [condition][path]
        List<bool> MatchMask,                    // [path] passes the whole WHERE
        bool Truncated);

    /// <summary>Has SQLite run the FROM…JOIN chain and report every joined row as the rowid it
    /// took from each table (NULL where an outer join found no match), along with whether each
    /// WHERE condition holds for it. That gives exact join semantics — LEFT/RIGHT/FULL/CROSS,
    /// compound ON clauses, NULL comparisons — without re-implementing them here.
    /// Returns null when a source has no rowid (a view or WITHOUT ROWID table).</summary>
    private static RowidJoin? TryJoinViaRowids(SqliteConnection c, List<string> tableNames,
        List<string?> aliases, string fromClause, string whereText, List<string> conditions)
    {
        int t = tableNames.Count;
        var rowids = Enumerable.Range(0, t).Select(k => $"{aliases[k] ?? tableNames[k]}.rowid").ToList();
        try { FetchRows(c, $"SELECT {string.Join(", ", rowids)} FROM {fromClause} LIMIT 0"); }
        catch (SqliteException) { return null; }

        // CASE so that NULL (unknown) counts as false, exactly as WHERE treats it. Any error
        // past this point is a genuine problem with the query, so let it surface.
        var tests = conditions.Select(cond => $"CASE WHEN ({cond}) THEN 1 ELSE 0 END").ToList();
        if (whereText.Length > 0) tests.Add($"CASE WHEN ({whereText}) THEN 1 ELSE 0 END");

        var tuples = new List<long?[]>();
        var flags = new List<bool[]>();
        bool truncated = false;
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = $"SELECT {string.Join(", ", rowids.Concat(tests))} FROM {fromClause}";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (tuples.Count >= MaxJoinRows) { truncated = true; break; }
                var tuple = new long?[t];
                for (int k = 0; k < t; k++) tuple[k] = reader.IsDBNull(k) ? null : reader.GetInt64(k);
                var f = new bool[tests.Count];
                for (int i = 0; i < f.Length; i++) f[i] = reader.GetInt64(t + i) != 0;
                tuples.Add(tuple);
                flags.Add(f);
            }
        }

        // Load each table (in rowid order) and translate rowids into row positions
        var tableCols = new List<List<ColumnHeader>>();
        var tableRows = new List<List<Dictionary<string, object?>>>();
        var rowIds = new List<List<long>>();
        var posOf = new List<Dictionary<long, int>>();
        for (int k = 0; k < t; k++)
        {
            var needed = tuples.Select(p => p[k]).OfType<long>().ToHashSet();
            var (cols, rows, ids, more) = FetchTableByRowid(c, tableNames[k], needed);
            tableCols.Add(cols);
            tableRows.Add(rows);
            rowIds.Add(ids);
            posOf.Add(ids.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i));
            truncated |= more;
        }
        var unsorted = tuples.Select(p => Enumerable.Range(0, t)
            .Select(k => p[k] is long id ? posOf[k][id] : JoinChainResult.NoMatch).ToArray()).ToList();

        // Present joined rows in source order (first table outermost), with rows that have
        // no partner on the left — RIGHT/FULL joins — after the rest, as SQLite emits them.
        var order = Enumerable.Range(0, unsorted.Count)
            .OrderBy(i => unsorted[i], Comparer<int[]>.Create(ComparePaths)).ToList();
        var paths = order.Select(i => unsorted[i]).ToList();
        var condResults = conditions.Select((_, ci) => order.Select(i => flags[i][ci]).ToList()).ToList();
        var mask = whereText.Length > 0
            ? order.Select(i => flags[i][conditions.Count]).ToList()
            : order.Select(_ => true).ToList();

        return new RowidJoin(tableCols, tableRows, rowIds, paths, condResults, mask, truncated);
    }

    private static int ComparePaths(int[] a, int[] b)
    {
        for (int k = 0; k < a.Length; k++)
        {
            int x = a[k] == JoinChainResult.NoMatch ? int.MaxValue : a[k];
            int y = b[k] == JoinChainResult.NoMatch ? int.MaxValue : b[k];
            if (x != y) return x.CompareTo(y);
        }
        return 0;
    }

    /// <summary>Fetches a table's first MaxJoinRows rows in rowid order, plus any later rows in
    /// <paramref name="required"/> (ones a joined row uses), returning each row's rowid.</summary>
    private static (List<ColumnHeader> Cols, List<Dictionary<string, object?>> Rows, List<long> RowIds, bool Truncated)
        FetchTableByRowid(SqliteConnection c, string table, HashSet<long> required)
    {
        var byId = new SortedDictionary<long, Dictionary<string, object?>>();
        var cols = new List<ColumnHeader>();
        bool more = false;

        void Read(string sql, int cap)
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = sql;
            using var reader = cmd.ExecuteReader();
            // Column 0 is the rowid; the table's own columns follow
            cols = Enumerable.Range(1, reader.FieldCount - 1).Select(i => new ColumnHeader(reader.GetName(i))).ToList();
            int n = 0;
            while (reader.Read())
            {
                if (n++ >= cap) { more = true; break; }
                var row = new Dictionary<string, object?>(cols.Count);
                for (int i = 0; i < cols.Count; i++)
                    row[cols[i].Name] = reader.IsDBNull(i + 1) ? null : reader.GetValue(i + 1);
                byId[reader.GetInt64(0)] = row;
            }
        }

        Read($"SELECT rowid, * FROM {table} ORDER BY rowid", MaxJoinRows);
        var missing = required.Where(id => !byId.ContainsKey(id)).ToList();
        if (missing.Count > 0)
            Read($"SELECT rowid, * FROM {table} WHERE rowid IN ({string.Join(",", missing)})", int.MaxValue);
        return (cols, byId.Values.ToList(), byId.Keys.ToList(), more);
    }

    /// <summary>The table columns an expression (e.g. a WHERE clause) reads, as
    /// alias-qualified (header, tableIndex, column) specs.</summary>
    private static List<(string Header, int T, string Col)> ReferencedColumns(
        string expr, List<string> tableNames, List<string?> aliases,
        List<List<ColumnHeader>> tableCols, Dictionary<string, int> refToIdx)
    {
        var found = new List<(string Header, int T, string Col)>();
        // Ignore anything inside string literals
        var code = Regex.Replace(expr, @"'(?:[^']|'')*'", "''");
        foreach (Match m in Regex.Matches(code, @"\b(?:(\w+)\.)?(\w+)\b"))
        {
            string r = m.Groups[1].Value, col = m.Groups[2].Value;
            int ti = r.Length > 0
                ? (refToIdx.TryGetValue(r, out var i) && ColInTable(tableCols[i], col) ? i : -1)
                : FindTableWithColumn(tableCols, col);
            if (ti < 0) continue;
            var name = tableCols[ti].First(h => string.Equals(h.Name, col, StringComparison.OrdinalIgnoreCase)).Name;
            if (!found.Any(f => f.T == ti && f.Col == name))
                found.Add(($"{aliases[ti] ?? tableNames[ti]}.{name}", ti, name));
        }
        return found;
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

    /// <summary>Resolves an equi-join ON condition into the prior-table index and its
    /// key column, plus the key column on the newly-joined table[newIdx]. Handles both
    /// qualified (t.col) and unqualified (col) operands, disambiguating bare columns by
    /// which table actually holds them. Falls back to the previous table if unresolved.</summary>
    private static (int LeftIdx, string? LeftKey, string? RightKey) ResolveJoinKeys(
        string onCondition, int newIdx,
        List<List<ColumnHeader>> tableCols, Dictionary<string, int> refToIdx)
    {
        var m = Regex.Match(onCondition, @"(?:(\w+)\.)?(\w+)\s*=\s*(?:(\w+)\.)?(\w+)");
        if (!m.Success) return (newIdx - 1, null, null);
        string aRef = m.Groups[1].Value, aCol = m.Groups[2].Value;
        string bRef = m.Groups[3].Value, bCol = m.Groups[4].Value;

        int RefIdx(string r) => r.Length > 0 && refToIdx.TryGetValue(r, out var i) ? i : -1;
        int aIdx = RefIdx(aRef), bIdx = RefIdx(bRef);

        // Decide which operand keys the newly-joined table: prefer an explicit ref, then
        // a ref pointing at a prior table (so the other side is the new one), and finally
        // fall back to column membership when both sides are unqualified.
        bool aIsNew;
        if (aIdx == newIdx) aIsNew = true;
        else if (bIdx == newIdx) aIsNew = false;
        else if (bIdx >= 0 && bIdx < newIdx) aIsNew = true;
        else if (aIdx >= 0 && aIdx < newIdx) aIsNew = false;
        else aIsNew = ColInTable(tableCols[newIdx], aCol) && !ColInTable(tableCols[newIdx], bCol);

        var (newCol, leftCol, leftRefIdx) = aIsNew ? (aCol, bCol, bIdx) : (bCol, aCol, aIdx);

        // Resolve the prior table for the left key when it wasn't qualified.
        int leftIdx = leftRefIdx;
        if (leftIdx < 0 || leftIdx >= newIdx)
            leftIdx = FindPriorTableWithColumn(tableCols, leftCol, newIdx);
        if (leftIdx < 0) leftIdx = newIdx - 1;

        return (leftIdx, leftCol, newCol);
    }

    private static bool ColInTable(List<ColumnHeader> cols, string col)
        => cols.Any(c => string.Equals(c.Name, col, StringComparison.OrdinalIgnoreCase));

    /// <summary>Finds a table with index &lt; beforeIdx that contains the given column.</summary>
    private static int FindPriorTableWithColumn(List<List<ColumnHeader>> tableCols, string col, int beforeIdx)
    {
        for (int k = beforeIdx - 1; k >= 0; k--)
            if (ColInTable(tableCols[k], col)) return k;
        return -1;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Splits a WHERE expression on top-level AND (respects parentheses, string
    /// literals, and the AND that belongs to <c>BETWEEN x AND y</c>).</summary>
    private static List<string> SplitTopLevelAnd(string expr)
    {
        static bool IsWordChar(char ch) => char.IsLetterOrDigit(ch) || ch == '_';

        var parts = new List<string>();
        int depth = 0;
        int start = 0;
        bool inBetween = false;
        for (int i = 0; i < expr.Length; i++)
        {
            char ch = expr[i];
            if (ch == '\'')
            {
                // Skip to the closing quote ('' is an escaped quote)
                int j = i + 1;
                while (j < expr.Length && !(expr[j] == '\'' && (j + 1 >= expr.Length || expr[j + 1] != '\'')))
                    j += expr[j] == '\'' ? 2 : 1;
                i = j;
            }
            else if (ch == '(') depth++;
            else if (ch == ')') depth--;
            else if (depth == 0 && IsWordChar(ch) && (i == 0 || !IsWordChar(expr[i - 1])))
            {
                int end = i;
                while (end < expr.Length && IsWordChar(expr[end])) end++;
                var word = expr[i..end];
                if (word.Equals("BETWEEN", StringComparison.OrdinalIgnoreCase)) inBetween = true;
                else if (word.Equals("AND", StringComparison.OrdinalIgnoreCase))
                {
                    if (inBetween) inBetween = false;
                    else { parts.Add(expr[start..i].Trim()); start = end; }
                }
                i = end - 1;
            }
        }
        parts.Add(expr[start..].Trim());
        return parts.Where(p => p.Length > 0).ToList();
    }

    /// <summary>Generates a stable string key for a row (for set membership tests).</summary>
    private static string RowKey(Dictionary<string, object?> row)
        => string.Join("|", row.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"));
}

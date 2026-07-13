using SqlVisualizer.Models;

namespace SqlVisualizer.Services;

/// <summary>
/// Converts an executor <see cref="VizResult"/> into a positioned node-graph
/// <see cref="VizScene"/> (nodes + edges + animation frames). Pure layout logic;
/// no SQL. The NodeCanvas renders whatever this produces.
/// </summary>
public class SceneBuilderService
{
    // Layout constants (world pixels)
    private const double CellW = 96, RowH = 28, HeaderH = 30, TitleH = 26,
                         RowGap = 6, ColGap = 200, Pad = 48, CardH = 44, CardGap = 12;

    public VizScene Build(VizResult r) => r switch
    {
        ScanResult s          => BuildScan(s),
        OrderByResult o       => BuildOrderBy(o),
        WhereOrderByResult wo => BuildWhere(new WhereResult(wo.Columns, wo.AllRows, wo.MatchMask,
                                                wo.WhereText, wo.Conditions, wo.ConditionResults),
                                            wo.SortedRows, wo.SortKeys),
        WhereResult w         => BuildWhere(w, null, null),
        JoinChainResult j     => BuildJoinChain(j),
        _                     => BuildEmpty()
    };

    // ── Shared helpers ───────────────────────────────────────────────────────

    private static double RowWidth(int nCols) => Math.Max(nCols * CellW, 120);

    private static List<string> CellsOf(Dictionary<string, object?> row, List<string> cols)
        => cols.Select(c => row.TryGetValue(c, out var v) ? (v?.ToString() ?? "NULL") : "").ToList();

    /// Builds a titled table (title + header + one node per row) at (x,y).
    private static (List<VizNode> Nodes, double W, double H, List<string> RowIds) MakeTable(
        string idPrefix, string title, List<ColumnHeader> cols,
        List<Dictionary<string, object?>> rows, double x, double y)
    {
        var colNames = cols.Select(c => c.Name).ToList();
        double w = RowWidth(colNames.Count);
        var nodes = new List<VizNode>
        {
            new($"{idPrefix}-title", "title", x, y, w, TitleH, new(), Title: title),
            new($"{idPrefix}-header", "header", x, y + TitleH, w, HeaderH, colNames),
        };
        var rowIds = new List<string>();
        double ry = y + TitleH + HeaderH;
        for (int i = 0; i < rows.Count; i++)
        {
            var id = $"{idPrefix}-row-{i}";
            rowIds.Add(id);
            nodes.Add(new VizNode(id, "row", x, ry, w, RowH, CellsOf(rows[i], colNames)));
            ry += RowH + RowGap;
        }
        return (nodes, w, ry - y, rowIds);
    }

    private static VizFrame F(Dictionary<string, string>? states = null,
                             HashSet<string>? hidden = null,
                             List<int>? edges = null,
                             string? phase = null)
        => new(states ?? new(), hidden ?? new(), edges ?? new(), phase);

    private static HashSet<string> HideFrom(List<string> ids, int revealedCount)
        => ids.Where((_, i) => i >= revealedCount).ToHashSet();

    private static VizScene Finish(string type, string title, string subtitle,
        List<VizNode> nodes, List<VizEdge> edges, List<VizFrame> frames)
    {
        double w = nodes.Count > 0 ? nodes.Max(n => n.X + n.W) + Pad : 400;
        double h = nodes.Count > 0 ? nodes.Max(n => n.Y + n.H) + Pad : 300;
        return new VizScene(type, title, subtitle, nodes, edges, frames, w, h);
    }

    private static string RowKey(Dictionary<string, object?> row)
        => string.Join("|", row.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"));

    /// Maps each row in `sorted` to a distinct index in `source` (duplicate-safe).
    private static List<int> MapToSource(List<Dictionary<string, object?>> sorted,
                                         List<Dictionary<string, object?>> source)
    {
        var used = new bool[source.Count];
        var result = new List<int>();
        foreach (var s in sorted)
        {
            var key = RowKey(s);
            int found = -1;
            for (int i = 0; i < source.Count; i++)
                if (!used[i] && RowKey(source[i]) == key) { used[i] = true; found = i; break; }
            result.Add(found);
        }
        return result;
    }

    // ── Scan (simple query) ──────────────────────────────────────────────────

    private VizScene BuildScan(ScanResult s)
    {
        var (nodes, _, _, rowIds) = MakeTable("t", "Rows (table scan)", s.Columns, s.Rows, Pad, Pad);
        int n = s.Rows.Count;
        var frames = new List<VizFrame>
        {
            F(rowIds.ToDictionary(id => id, _ => "pending"), phase: "Ready to scan")
        };
        for (int i = 0; i < n; i++)
        {
            var st = new Dictionary<string, string>();
            for (int j = 0; j < n; j++)
                st[rowIds[j]] = j < i ? "read" : j == i ? "scanning" : "pending";
            frames.Add(F(st, phase: $"Reading row {i + 1} of {n}"));
        }
        frames.Add(F(rowIds.ToDictionary(id => id, _ => "read"), phase: "Scan complete"));

        if (s.Limit is int lim && lim < n)
        {
            var cut = new Dictionary<string, string>();
            for (int j = 0; j < n; j++) cut[rowIds[j]] = j < lim ? "kept" : "cut";
            frames.Add(F(cut, phase: $"LIMIT {lim} — keep first {lim} row(s)"));
        }

        var sub = s.Limit is int l ? $"full scan · LIMIT {l}" : "full table scan";
        return Finish("scan", "Table Scan", sub, nodes, new(), frames);
    }

    // ── ORDER BY ─────────────────────────────────────────────────────────────

    private VizScene BuildOrderBy(OrderByResult o)
    {
        var (lNodes, lw, _, lRowIds) = MakeTable("in", "Input (unsorted)", o.Columns, o.UnsortedRows, Pad, Pad);
        double rightX = Pad + lw + ColGap;
        var sortedTitle = "Sorted by " + string.Join(", ", o.SortKeys);
        var (rNodes, _, _, rRowIds) = MakeTable("out", sortedTitle, o.Columns, o.SortedRows, rightX, Pad);
        var nodes = lNodes.Concat(rNodes).ToList();

        var srcIdx = MapToSource(o.SortedRows, o.UnsortedRows);
        var edges = new List<VizEdge>();
        for (int i = 0; i < rRowIds.Count; i++)
        {
            int from = srcIdx[i] >= 0 ? srcIdx[i] : Math.Min(i, lRowIds.Count - 1);
            edges.Add(new VizEdge(lRowIds[from], rRowIds[i]));
        }

        int m = rRowIds.Count;
        var frames = new List<VizFrame> { F(hidden: HideFrom(rRowIds, 0), phase: "Input (unsorted)") };
        for (int i = 0; i < m; i++)
        {
            var st = new Dictionary<string, string>();
            for (int j = 0; j < m; j++)
            {
                if (srcIdx[j] >= 0) st[lRowIds[srcIdx[j]]] = j < i ? "placed" : j == i ? "active" : "";
                st[rRowIds[j]] = j < i ? "placed" : j == i ? "new" : "";
            }
            frames.Add(F(st, HideFrom(rRowIds, i + 1), new() { i },
                phase: $"Placing row {i + 1} of {m}"));
        }
        var final = new Dictionary<string, string>();
        foreach (var id in lRowIds) final[id] = "placed";
        foreach (var id in rRowIds) final[id] = "placed";
        frames.Add(F(final, phase: "Sorted"));

        if (o.Limit is int lim && lim < m)
        {
            var cut = new Dictionary<string, string>(final);
            for (int j = 0; j < m; j++) cut[rRowIds[j]] = j < lim ? "kept" : "cut";
            frames.Add(F(cut, phase: $"LIMIT {lim} — keep first {lim} row(s)"));
        }

        var sub = "ORDER BY " + string.Join(", ", o.SortKeys) + (o.Limit is int l ? $" · LIMIT {l}" : "");
        return Finish("order_by", "ORDER BY", sub, nodes, edges, frames);
    }

    // ── WHERE (and WHERE + ORDER BY) ─────────────────────────────────────────

    private VizScene BuildWhere(WhereResult w,
        List<Dictionary<string, object?>>? sortedRows, List<string>? sortKeys)
    {
        var nodes = new List<VizNode>();
        int condCount = w.Conditions.Count;

        // Condition cards across the top
        var cardIds = new List<string>();
        double cx = Pad;
        for (int ci = 0; ci < condCount; ci++)
        {
            double cw = Math.Max(w.Conditions[ci].Length * 7 + 44, 150);
            var id = $"card-{ci}";
            cardIds.Add(id);
            nodes.Add(new VizNode(id, "card", cx, Pad, cw, CardH, new(), Badge: w.Conditions[ci]));
            cx += cw + CardGap;
        }
        double inputY = Pad + (condCount > 0 ? CardH + 28 : 0);

        // Input table
        var (inNodes, inW, _, inRowIds) = MakeTable("all", "Rows", w.Columns, w.AllRows, Pad, inputY);
        nodes.AddRange(inNodes);

        // Matched output table
        var matchedRows = w.AllRows.Where((_, i) => w.MatchMask[i]).ToList();
        double matchedX = Pad + inW + ColGap;
        var (mNodes, mW, _, mRowIds) = MakeTable("match", "Matched (WHERE)", w.Columns, matchedRows, matchedX, inputY);
        nodes.AddRange(mNodes);

        // matched output index per input row
        var matchedIdx = new int[w.AllRows.Count];
        int cnt = 0;
        for (int i = 0; i < w.AllRows.Count; i++)
        {
            matchedIdx[i] = w.MatchMask[i] ? cnt : -1;
            if (w.MatchMask[i]) cnt++;
        }

        var edges = new List<VizEdge>();
        var inToMatch = new Dictionary<int, int>(); // edge index by matched output slot
        for (int i = 0; i < w.AllRows.Count; i++)
            if (matchedIdx[i] >= 0)
            {
                inToMatch[matchedIdx[i]] = edges.Count;
                edges.Add(new VizEdge(inRowIds[i], mRowIds[matchedIdx[i]]));
            }

        // Optional sorted output table
        List<string> sRowIds = new();
        var sortToMatch = new Dictionary<int, int>();
        if (sortedRows != null)
        {
            double sortedX = matchedX + mW + ColGap;
            var sortedTitle = "Sorted by " + string.Join(", ", sortKeys ?? new());
            var (sNodes, _, _, ids) = MakeTable("sort", sortedTitle, w.Columns, sortedRows, sortedX, inputY);
            nodes.AddRange(sNodes);
            sRowIds = ids;
            var map = MapToSource(sortedRows, matchedRows);
            for (int i = 0; i < sRowIds.Count; i++)
            {
                int from = map[i] >= 0 ? map[i] : Math.Min(i, mRowIds.Count - 1);
                sortToMatch[i] = edges.Count;
                edges.Add(new VizEdge(mRowIds[from], sRowIds[i]));
            }
        }

        // Frames
        var frames = new List<VizFrame>();
        int n = w.AllRows.Count;

        HashSet<string> HideOutputs(int matchedShown, int sortedShown)
        {
            var h = HideFrom(mRowIds, matchedShown);
            h.UnionWith(HideFrom(sRowIds, sortedShown));
            return h;
        }

        frames.Add(F(inRowIds.ToDictionary(id => id, _ => "pending"),
            HideOutputs(0, 0), phase: "Start scan"));

        int matchedShownSoFar = 0;
        for (int i = 0; i < n; i++)
        {
            // helper to mark already-decided prior rows
            void PriorStates(Dictionary<string, string> st)
            {
                for (int p = 0; p < i; p++)
                    st[inRowIds[p]] = w.MatchMask[p] ? "matched" : "rejected";
            }

            // scan frame
            var scan = new Dictionary<string, string>();
            PriorStates(scan);
            scan[inRowIds[i]] = "scanning";
            frames.Add(F(scan, HideOutputs(matchedShownSoFar, 0), phase: $"Scan row {i + 1} of {n}"));

            // per-condition frames
            for (int ci = 0; ci < condCount; ci++)
            {
                var cst = new Dictionary<string, string>();
                PriorStates(cst);
                cst[inRowIds[i]] = "scanning";
                for (int cj = 0; cj <= ci; cj++)
                    cst[cardIds[cj]] = (w.ConditionResults[cj].Count > i && w.ConditionResults[cj][i]) ? "true" : "false";
                frames.Add(F(cst, HideOutputs(matchedShownSoFar, 0), phase: $"Check: {w.Conditions[ci]}"));
            }

            // result frame
            if (matchedIdx[i] >= 0) matchedShownSoFar = matchedIdx[i] + 1;
            var res = new Dictionary<string, string>();
            PriorStates(res);
            res[inRowIds[i]] = w.MatchMask[i] ? "matched" : "rejected";
            var resEdges = new List<int>();
            if (matchedIdx[i] >= 0)
            {
                res[mRowIds[matchedIdx[i]]] = "new";
                if (inToMatch.TryGetValue(matchedIdx[i], out var ei)) resEdges.Add(ei);
            }
            frames.Add(F(res, HideOutputs(matchedShownSoFar, 0), resEdges,
                phase: w.MatchMask[i] ? "✓ matches" : "✗ filtered out"));
        }

        // all-decided frame
        var decided = new Dictionary<string, string>();
        for (int i = 0; i < n; i++) decided[inRowIds[i]] = w.MatchMask[i] ? "matched" : "rejected";
        foreach (var id in mRowIds) decided[id] = "matched";
        frames.Add(F(decided, HideOutputs(mRowIds.Count, 0), phase: "Filter complete"));

        // sort phase
        if (sortedRows != null)
        {
            var baseStates = new Dictionary<string, string>(decided);
            for (int i = 0; i < sRowIds.Count; i++)
            {
                var st = new Dictionary<string, string>(baseStates);
                for (int j = 0; j <= i; j++) st[sRowIds[j]] = j == i ? "new" : "placed";
                var e = sortToMatch.TryGetValue(i, out var ei) ? new List<int> { ei } : new();
                frames.Add(F(st, HideOutputs(mRowIds.Count, i + 1), e,
                    phase: $"Sort row {i + 1} of {sRowIds.Count}"));
            }
            var fin = new Dictionary<string, string>(decided);
            foreach (var id in sRowIds) fin[id] = "placed";
            frames.Add(F(fin, phase: "Sorted"));
        }

        var type = sortedRows != null ? "where_order_by" : "where";
        var title = sortedRows != null ? "WHERE + ORDER BY" : "WHERE";
        var sub = $"WHERE {w.WhereText}";
        return Finish(type, title, sub, nodes, edges, frames);
    }

    // ── JOIN chain (1..N joins) ──────────────────────────────────────────────

    private VizScene BuildJoinChain(JoinChainResult j)
    {
        var nodes = new List<VizNode>();
        int t = j.TableNames.Count;

        // Lay tables left→right, tracking each table's row-node ids and x offset
        var tableRowIds = new List<List<string>>();
        double x = Pad;
        for (int k = 0; k < t; k++)
        {
            var label = j.TableNames[k] + (j.Aliases[k] != null ? $" ({j.Aliases[k]})" : "");
            var (tn, tw, _, ids) = MakeTable($"t{k}", label, j.TableColumns[k], j.TableRows[k], x, Pad);
            nodes.AddRange(tn);
            tableRowIds.Add(ids);
            x += tw + ColGap;
        }

        // Merged result table at the far right
        var (mNodes, _, _, mRowIds) = MakeTable("merged", "Joined Result", j.MergedColumns, j.MergedRows, x, Pad);
        nodes.AddRange(mNodes);

        // One connector per path segment, grouped by output row so we can light
        // up exactly the source rows that combine to form each joined row.
        var edges = new List<VizEdge>();
        var pathEdges = new List<List<int>>();
        for (int i = 0; i < j.JoinPaths.Count; i++)
        {
            var p = j.JoinPaths[i];
            var segs = new List<int>();
            for (int k = 0; k < t - 1; k++)
            {
                int L = p[k], R = p[k + 1];
                if (L < tableRowIds[k].Count && R < tableRowIds[k + 1].Count)
                {
                    segs.Add(edges.Count);
                    edges.Add(new VizEdge(tableRowIds[k][L], tableRowIds[k + 1][R]));
                }
            }
            pathEdges.Add(segs);
        }

        int mm = mRowIds.Count;
        var frames = new List<VizFrame>
        {
            F(hidden: HideFrom(mRowIds, 0), phase: $"Ready — {mm} joined row(s)")
        };
        var used = new HashSet<string>();
        for (int i = 0; i < mm; i++)
        {
            var p = j.JoinPaths[i];

            // Light the source rows that form this joined row; keep prior ones dim-green
            var active = new Dictionary<string, string>();
            foreach (var id in used) active[id] = "matched";
            for (int k = 0; k < t; k++)
                if (p[k] < tableRowIds[k].Count) active[tableRowIds[k][p[k]]] = "active";

            // Forming: draw the connectors across the tables, result row not yet added
            frames.Add(F(new Dictionary<string, string>(active), HideFrom(mRowIds, i),
                pathEdges[i], phase: $"Forming joined row {i + 1} of {mm}"));

            // Emit: the completed row drops into the joined result table
            var emit = new Dictionary<string, string>(active) { [mRowIds[i]] = "new" };
            frames.Add(F(emit, HideFrom(mRowIds, i + 1), pathEdges[i],
                phase: $"Add joined row {i + 1} to result"));

            for (int k = 0; k < t; k++)
                if (p[k] < tableRowIds[k].Count) used.Add(tableRowIds[k][p[k]]);
        }

        var final = used.ToDictionary(id => id, _ => "matched");
        foreach (var id in mRowIds) final[id] = "matched";
        frames.Add(F(final, phase: "Join complete"));

        var joinDesc = string.Join(" ⋈ ", j.TableNames);
        var onDesc = string.Join("  ·  ", j.Steps.Select(s => s.OnCondition));
        return Finish("join_chain", joinDesc, onDesc, nodes, edges, frames);
    }

    private VizScene BuildEmpty()
        => new("empty", "Nothing to visualize",
               "This query has no visualizable structure.",
               new(), new(), new() { F(phase: "—") }, 400, 200);
}

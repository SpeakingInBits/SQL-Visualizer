using Microsoft.Data.Sqlite;
using SqlVisualizer.Models;
using SqlVisualizer.Services;

namespace SqlVisualizer.Tests;

/// <summary>A fresh in-memory copy of one of the app's sample databases.</summary>
public sealed class SampleDb : IDisposable
{
    private readonly SqliteConnectionService _conn = new();

    public QueryExecutorService Executor { get; }

    public SampleDb(string sample)
    {
        new SampleDatabaseService(_conn).OpenSample(sample);
        Executor = new QueryExecutorService(_conn);
    }

    public VizResult Visualize(string sql) => Executor.VisualizeQuery(sql);

    public VizScene Scene(string sql) => new SceneBuilderService().Build(Visualize(sql));

    /// <summary>What SQLite itself returns for a query, formatted the way the canvas
    /// renders cells. Read by ordinal, so duplicate column names survive.</summary>
    public List<List<string>> Query(string sql)
    {
        using var cmd = _conn.Require().CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        var rows = new List<List<string>>();
        while (reader.Read())
            rows.Add(Enumerable.Range(0, reader.FieldCount)
                .Select(i => reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString() ?? "")
                .ToList());
        return rows;
    }

    public void Run(string sql) => Executor.RunStatement(sql);

    public void Dispose() => _conn.Dispose();
}

/// <summary>Assertions over a rendered <see cref="VizScene"/> — what the canvas would draw.</summary>
public static class SceneAssert
{
    /// <summary>Every id a frame or edge refers to exists, and every frame is renderable.</summary>
    public static void WellFormed(VizScene scene)
    {
        var ids = scene.Nodes.Select(n => n.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        var idSet = ids.ToHashSet();

        foreach (var e in scene.Edges)
        {
            Assert.Contains(e.FromId, idSet);
            Assert.Contains(e.ToId, idSet);
        }

        Assert.NotEmpty(scene.Frames);
        for (int f = 0; f < scene.Frames.Count; f++)
        {
            var frame = scene.Frames[f];
            foreach (var id in frame.States.Keys)
                Assert.True(idSet.Contains(id), $"frame {f} sets state on unknown node '{id}'");
            foreach (var id in frame.Hidden)
                Assert.True(idSet.Contains(id), $"frame {f} hides unknown node '{id}'");
            foreach (var ei in frame.ActiveEdges)
                Assert.InRange(ei, 0, scene.Edges.Count - 1);
            // An active edge must connect two nodes that are drawn in that frame
            foreach (var ei in frame.ActiveEdges)
            {
                Assert.DoesNotContain(scene.Edges[ei].FromId, frame.Hidden);
                Assert.DoesNotContain(scene.Edges[ei].ToId, frame.Hidden);
            }
            Assert.False(string.IsNullOrWhiteSpace(frame.Phase), $"frame {f} has no phase label");
        }

        foreach (var n in scene.Nodes)
        {
            Assert.InRange(n.X + n.W, 0, scene.Width);
            Assert.InRange(n.Y + n.H, 0, scene.Height);
        }
    }

    /// <summary>The node-id prefix of the table holding the query's final rows.</summary>
    public static string OutputTable(VizScene scene) => scene.VizType switch
    {
        "join_chain"     => scene.Nodes.Any(n => n.Id.StartsWith("result-")) ? "result" : "merged",
        "where"          => "match",
        "where_order_by" => "sort",
        "order_by"       => "out",
        "scan"           => "t",
        var other        => throw new InvalidOperationException($"no output table for '{other}'")
    };

    /// <summary>Cells of the output table's rows as drawn on the last frame (LIMIT-cut rows excluded).</summary>
    public static List<List<string>> FinalRows(VizScene scene, string? table = null)
    {
        var prefix = (table ?? OutputTable(scene)) + "-row-";
        var last = scene.Frames[^1];
        return scene.Nodes
            .Where(n => n.Kind == "row" && n.Id.StartsWith(prefix))
            .Where(n => !last.Hidden.Contains(n.Id))
            .Where(n => !(last.States.TryGetValue(n.Id, out var s) && s == "cut"))
            .Select(n => n.Cells)
            .ToList();
    }

    public static List<string> Header(VizScene scene, string table)
        => scene.Nodes.Single(n => n.Id == $"{table}-header").Cells;

    /// <summary>The final output shows exactly the rows SQLite returns (order-insensitive).</summary>
    public static void ShowsQueryResult(SampleDb db, VizScene scene, string sql)
    {
        static List<string> Canon(IEnumerable<List<string>> rows)
            => rows.Select(r => string.Join(" | ", r)).OrderBy(s => s, StringComparer.Ordinal).ToList();

        Assert.Equal(Canon(db.Query(sql)), Canon(FinalRows(scene)));
    }

    public static string StateOf(VizFrame frame, string nodeId)
        => frame.States.TryGetValue(nodeId, out var s) ? s : "";
}

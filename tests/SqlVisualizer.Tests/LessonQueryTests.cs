using System.Text.Json;
using System.Text.RegularExpressions;
using SqlVisualizer.Models;
using SqlVisualizer.Services;

namespace SqlVisualizer.Tests;

/// <summary>
/// Visualizes every SQL example and practice solution in the course, so a change to
/// the visualizer can't silently break a lesson. Statements run in lesson order on a
/// fresh copy of that lesson's database, so DDL/DML lessons see the state they expect.
/// </summary>
public class LessonQueryTests
{
    private static readonly string LessonsDir = Path.Combine(AppContext.BaseDirectory, "lessons");

    public record LessonQuery(string Lesson, string Database, int Index, string Sql, List<string> Before)
    {
        public override string ToString()
        {
            var oneLine = Regex.Replace(Sql, @"\s+", " ");
            return $"{Lesson} #{Index}: {oneLine[..Math.Min(oneLine.Length, 90)]}";
        }
    }

    public static IEnumerable<object[]> Queries()
    {
        foreach (var dir in Directory.GetDirectories(LessonsDir).OrderBy(d => d))
        {
            var lesson = Path.GetFileName(dir);
            using var meta = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "meta.json")));
            if (!meta.RootElement.TryGetProperty("database", out var dbProp)) continue;
            var db = dbProp.GetString()!;

            var statements = new List<string>();
            var content = Path.Combine(dir, "content.md");
            if (File.Exists(content))
                foreach (Match m in Regex.Matches(File.ReadAllText(content), @"```sql\r?\n([\s\S]*?)\r?\n```"))
                    statements.AddRange(SplitStatements(m.Groups[1].Value));

            var practice = Path.Combine(dir, "practice.json");
            if (File.Exists(practice))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(practice));
                foreach (var p in doc.RootElement.GetProperty("problems").EnumerateArray())
                    if (p.TryGetProperty("solution", out var sol))
                        statements.AddRange(SplitStatements(sol.GetString()!));
            }

            for (int i = 0; i < statements.Count; i++)
                if (QueryExecutorService.IsVisualizable(statements[i]))
                    yield return new object[] { new LessonQuery(lesson, db, i, statements[i], statements.Take(i).ToList()) };
        }
    }

    private static IEnumerable<string> SplitStatements(string sql)
        => Regex.Replace(sql, @"--[^\n]*", "")
            .Split(';').Select(s => s.Trim()).Where(s => s.Length > 0);

    [Fact]
    public void FindsTheCourseContent()
        => Assert.True(Queries().Count() > 100, "expected the lesson SQL to be copied next to the tests");

    [Theory]
    [MemberData(nameof(Queries))]
    public void LessonQuery_Visualizes(LessonQuery q)
    {
        using var db = new SampleDb(q.Database);
        foreach (var stmt in q.Before.Where(s => !QueryExecutorService.IsVisualizable(s)))
            try { db.Run(stmt); } catch { /* lessons demo failing statements too */ }

        // A query that's an error in SQLite is allowed to fail to visualize
        try { db.Query(q.Sql); } catch { return; }

        if (IsKnownUnsupported(q.Sql))
        {
            var ex = Record.Exception(() => SceneAssert.WellFormed(db.Scene(q.Sql)));
            Assert.True(ex != null, "This query visualizes now — remove it from KnownUnsupported so it stays covered.");
            return;
        }

        var result = db.Visualize(q.Sql);
        var scene = new SceneBuilderService().Build(result);
        SceneAssert.WellFormed(scene);

        // Join visualizations must end on exactly the rows the query returns, when the
        // query is one the visualizer models (no grouping, set operators or subqueries)
        // and fits under its row cap.
        if (result is JoinChainResult { Truncated: false } && IsPlainJoin(q.Sql))
            SceneAssert.ShowsQueryResult(db, scene, q.Sql);
    }

    /// <summary>Lesson queries the visualizer can't handle yet (matched by prefix). Each is
    /// expected to fail; once one works, delete its entry.</summary>
    private static readonly string[] KnownUnsupported =
    [
        // Scalar subquery in the SELECT list that has its own WHERE
        "SELECT d.name, (SELECT COUNT(*) FROM employees e WHERE e.dept_id = d.id) AS headcount",
        "SELECT p.name, (SELECT SUM(a.hours) FROM assignments a WHERE a.project_id = p.id)",
        // Derived table in FROM, joined to a base table
        "SELECT d.name, ROUND(t.avg_sal, 2) AS avg_salary FROM (SELECT",
        "SELECT p.name, t.total_hours FROM (SELECT",
        // IN (subquery … GROUP BY …)
        "SELECT name FROM employees WHERE id IN (SELECT employee_id FROM assignments GROUP BY",
        "SELECT name FROM departments WHERE id IN (SELECT dept_id FROM projects GROUP BY",
    ];

    private static bool IsKnownUnsupported(string sql)
    {
        var oneLine = Regex.Replace(sql, @"\s+", " ");
        return KnownUnsupported.Any(k => oneLine.StartsWith(k, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPlainJoin(string sql)
        => !Regex.IsMatch(sql, @"\b(GROUP\s+BY|HAVING|DISTINCT|UNION|INTERSECT|EXCEPT|LIMIT|COUNT|SUM|AVG|MIN|MAX)\b|\(\s*SELECT",
            RegexOptions.IgnoreCase);
}

using SqlVisualizer.Models;

namespace SqlVisualizer.Tests;

/// <summary>JOIN visualizations against the school sample database (the Joins lesson's).</summary>
public class JoinVisualizationTests : IDisposable
{
    private readonly SampleDb _db = new("school");

    public void Dispose() => _db.Dispose();

    private (JoinChainResult Result, VizScene Scene) Viz(string sql)
    {
        var result = Assert.IsType<JoinChainResult>(_db.Visualize(sql));
        var scene = new SqlVisualizer.Services.SceneBuilderService().Build(result);
        SceneAssert.WellFormed(scene);
        return (result, scene);
    }

    // ── Inner joins ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("SELECT s.name, e.grade FROM students s JOIN enrollments e ON e.student_id = s.id;")]
    [InlineData("SELECT s.name, c.title FROM students s JOIN enrollments e ON e.student_id = s.id JOIN courses c ON c.id = e.course_id;")]
    [InlineData("SELECT * FROM students s INNER JOIN enrollments e ON s.id = e.student_id")]
    [InlineData("SELECT s.name AS student, m.name AS mentor FROM students s JOIN students m ON s.mentor_id = m.id;")]
    public void InnerJoin_ShowsExactlyTheQueryResult(string sql)
    {
        var (result, scene) = Viz(sql);
        Assert.Null(result.Filter);
        Assert.DoesNotContain(result.JoinPaths, p => p.Contains(JoinChainResult.NoMatch));
        SceneAssert.ShowsQueryResult(_db, scene, sql);
    }

    [Fact]
    public void InnerJoin_DropsRowsWithoutAMatch()
    {
        var (_, scene) = Viz("SELECT s.name, e.grade FROM students s JOIN enrollments e ON e.student_id = s.id;");
        Assert.DoesNotContain(SceneAssert.FinalRows(scene), r => r[0] == "Ivy");
        Assert.DoesNotContain(scene.Frames, f => f.Phase!.StartsWith("No match"));
    }

    // ── Outer joins (issue #14) ──────────────────────────────────────────────

    private const string LeftJoin =
        "SELECT s.name, e.course_id, e.grade FROM students s LEFT JOIN enrollments e ON e.student_id = s.id;";

    [Fact]
    public void LeftJoin_KeepsTheUnmatchedRowWithNulls()
    {
        var (result, scene) = Viz(LeftJoin);

        SceneAssert.ShowsQueryResult(_db, scene, LeftJoin);
        Assert.Contains(new List<string> { "Ivy", "NULL", "NULL" }, SceneAssert.FinalRows(scene));

        // Ivy (students row 8) is joined to no enrollment
        var ivy = Assert.Single(result.JoinPaths, p => p[1] == JoinChainResult.NoMatch);
        Assert.Equal("Ivy", result.TableRows[0][ivy[0]]["name"]);
    }

    [Fact]
    public void LeftJoin_AnimatesTheUnmatchedRow()
    {
        var (result, scene) = Viz(LeftJoin);
        int ivy = result.JoinPaths.FindIndex(p => p[1] == JoinChainResult.NoMatch);
        string ivySource = $"t0-row-{result.JoinPaths[ivy][0]}", ivyJoined = $"merged-row-{ivy}";

        var forming = Assert.Single(scene.Frames, f => f.Phase!.StartsWith("No match in enrollments"));
        Assert.Equal("outer", SceneAssert.StateOf(forming, ivySource));
        Assert.Empty(forming.ActiveEdges);                  // nothing on the right to connect to
        Assert.Contains(ivyJoined, forming.Hidden);         // not added yet

        var emit = scene.Frames[scene.Frames.IndexOf(forming) + 1];
        Assert.StartsWith("Add joined row", emit.Phase);
        Assert.Contains("NULLs for enrollments", emit.Phase);
        Assert.Equal("new", SceneAssert.StateOf(emit, ivyJoined));

        var last = scene.Frames[^1];
        Assert.Equal("outer", SceneAssert.StateOf(last, ivyJoined));
        Assert.Contains("1 with NULLs", last.Phase);
        Assert.Equal("students LEFT JOIN enrollments", scene.Title);
    }

    [Fact]
    public void LeftJoin_FilteringForNulls_FindsRowsWithNoMatch()
    {
        const string sql = "SELECT s.name FROM students s LEFT JOIN enrollments e ON e.student_id = s.id WHERE e.id IS NULL;";
        var (result, scene) = Viz(sql);

        Assert.Equal(new[] { new List<string> { "Ivy" } }, SceneAssert.FinalRows(scene));
        SceneAssert.ShowsQueryResult(_db, scene, sql);

        // The WHERE runs over all 14 joined rows (13 enrollments + Ivy's NULL row)…
        var filter = Assert.IsType<JoinFilter>(result.Filter);
        Assert.Equal(14, result.MergedRows.Count);
        Assert.Equal(1, filter.MatchMask.Count(m => m));
        // …and the joined rows show e.id, the column it tests, though the SELECT doesn't
        Assert.Equal(new[] { "name", "e.id" }, SceneAssert.Header(scene, "merged"));
        Assert.Equal(new[] { "name" }, SceneAssert.Header(scene, "result"));
    }

    [Fact]
    public void RightJoin_KeepsUnmatchedRightRows()
    {
        const string sql = "SELECT s.name, a.name FROM students s RIGHT JOIN alumni a ON s.name = a.name;";
        var (result, scene) = Viz(sql);

        SceneAssert.ShowsQueryResult(_db, scene, sql);
        Assert.Equal(3, result.JoinPaths.Count(p => p[0] == JoinChainResult.NoMatch));
        Assert.Equal(3, scene.Frames.Count(f => f.Phase!.StartsWith("No match in students")));
    }

    [Fact]
    public void FullOuterJoin_KeepsUnmatchedRowsFromBothSides()
    {
        const string sql = "SELECT s.name AS student_name, a.name AS alumni_name FROM students s FULL OUTER JOIN alumni a ON s.name = a.name;";
        var (result, scene) = Viz(sql);

        SceneAssert.ShowsQueryResult(_db, scene, sql);
        Assert.Equal(8, result.JoinPaths.Count(p => p[1] == JoinChainResult.NoMatch));  // students w/o alumni
        Assert.Equal(3, result.JoinPaths.Count(p => p[0] == JoinChainResult.NoMatch));  // alumni w/o students
        // Left-unmatched rows come after everything that has a left row
        int firstRightOnly = result.JoinPaths.FindIndex(p => p[0] == JoinChainResult.NoMatch);
        Assert.All(result.JoinPaths.Skip(firstRightOnly), p => Assert.Equal(JoinChainResult.NoMatch, p[0]));
        Assert.Equal("students FULL OUTER JOIN alumni", scene.Title);
    }

    [Fact]
    public void SelfLeftJoin_KeepsStudentsWithoutAMentor()
    {
        const string sql = "SELECT s.name AS student, m.name AS mentor FROM students s LEFT JOIN students m ON s.mentor_id = m.id;";
        var (_, scene) = Viz(sql);

        SceneAssert.ShowsQueryResult(_db, scene, sql);
        Assert.Equal(new[] { "Carol", "Frank", "Grace" },
            SceneAssert.FinalRows(scene).Where(r => r[1] == "NULL").Select(r => r[0]).OrderBy(n => n));
        // Self-join phase text names the alias, not the (ambiguous) table name
        Assert.Contains(scene.Frames, f => f.Phase!.StartsWith("No match in m "));
    }

    [Fact]
    public void LeftJoin_WithCompoundOnCondition_UsesTheWholeCondition()
    {
        // Only A grades join; students without one are kept with NULLs.
        const string sql = "SELECT s.name, e.grade FROM students s LEFT JOIN enrollments e ON e.student_id = s.id AND e.grade = 'A';";
        var (_, scene) = Viz(sql);
        SceneAssert.ShowsQueryResult(_db, scene, sql);
    }

    [Fact]
    public void LeftJoinChain_KeepsUnmatchedRowsThroughEveryJoin()
    {
        const string sql = "SELECT s.name, c.title FROM students s LEFT JOIN enrollments e ON e.student_id = s.id LEFT JOIN courses c ON c.id = e.course_id;";
        var (result, scene) = Viz(sql);
        SceneAssert.ShowsQueryResult(_db, scene, sql);
        var ivy = Assert.Single(result.JoinPaths, p => p[1] == JoinChainResult.NoMatch);
        Assert.Equal(JoinChainResult.NoMatch, ivy[2]);
    }

    [Fact]
    public void CrossJoin_PairsEveryRow()
    {
        const string sql = "SELECT c.title, a.name FROM courses c CROSS JOIN alumni a;";
        var (result, scene) = Viz(sql);
        Assert.Equal(6 * 4, result.MergedRows.Count);
        SceneAssert.ShowsQueryResult(_db, scene, sql);
    }

    // ── WHERE on joins (issue #12) ───────────────────────────────────────────

    [Fact]
    public void JoinWithWhere_ShowsOnlyRowsThatPassTheFilter()
    {
        const string sql = "SELECT s.name, c.title, e.grade FROM students s JOIN enrollments e ON e.student_id = s.id JOIN courses c ON c.id = e.course_id WHERE e.grade = 'A';";
        var (result, scene) = Viz(sql);

        SceneAssert.ShowsQueryResult(_db, scene, sql);
        var filter = Assert.IsType<JoinFilter>(result.Filter);
        Assert.Equal(13, result.MergedRows.Count);              // every enrollment joins…
        Assert.Equal(7, filter.MatchMask.Count(m => m));         // …seven are A's
        Assert.All(SceneAssert.FinalRows(scene), r => Assert.Equal("A", r[2]));
    }

    [Fact]
    public void JoinWithWhere_DisplaysTheFilterCondition()
    {
        const string sql = "SELECT s.name, c.title FROM students s JOIN enrollments e ON e.student_id = s.id JOIN courses c ON c.id = e.course_id WHERE c.title = 'Biology';";
        var (_, scene) = Viz(sql);

        Assert.Contains("WHERE c.title = 'Biology'", scene.Subtitle);
        var card = Assert.Single(scene.Nodes, n => n.Kind == "card");
        Assert.Equal("c.title = 'Biology'", card.Badge);

        // The card flips TRUE/FALSE as each joined row is tested
        var checks = scene.Frames.Where(f => f.Phase == "Check: c.title = 'Biology'").ToList();
        Assert.Equal(13, checks.Count);
        Assert.Equal(4, checks.Count(f => SceneAssert.StateOf(f, card.Id) == "true"));
        Assert.Equal(9, checks.Count(f => SceneAssert.StateOf(f, card.Id) == "false"));
        Assert.Equal(4, scene.Frames.Count(f => f.Phase == "✓ kept by WHERE"));
        Assert.Equal(9, scene.Frames.Count(f => f.Phase == "✗ filtered out by WHERE"));
        Assert.Equal("Filter complete — 4 of 13 row(s) kept", scene.Frames[^1].Phase);
    }

    [Fact]
    public void JoinWithWhere_FilterPhaseRunsAfterTheJoin()
    {
        var (_, scene) = Viz("SELECT s.name FROM students s JOIN enrollments e ON e.student_id = s.id WHERE e.grade = 'C';");
        var phases = scene.Frames.Select(f => f.Phase!).ToList();
        int joinDone = phases.FindIndex(p => p.StartsWith("Join complete"));
        int firstTest = phases.FindIndex(p => p.StartsWith("WHERE: test"));
        Assert.InRange(joinDone, 1, firstTest - 1);
        Assert.DoesNotContain(phases.Take(joinDone), p => p.StartsWith("Check:"));

        // Result rows stay hidden until the filter lets them through
        var joinDoneFrame = scene.Frames[joinDone];
        Assert.All(scene.Nodes.Where(n => n.Id.StartsWith("result-row-")),
            n => Assert.Contains(n.Id, joinDoneFrame.Hidden));
    }

    [Fact]
    public void JoinWithMultipleConditions_ChecksEachOne()
    {
        const string sql = "SELECT s.name, c.title FROM students s JOIN enrollments e ON e.student_id = s.id JOIN courses c ON c.id = e.course_id WHERE e.grade = 'A' AND c.credits >= 4;";
        var (result, scene) = Viz(sql);

        SceneAssert.ShowsQueryResult(_db, scene, sql);
        var filter = Assert.IsType<JoinFilter>(result.Filter);
        Assert.Equal(new[] { "e.grade = 'A'", "c.credits >= 4" }, filter.Conditions);
        for (int i = 0; i < result.MergedRows.Count; i++)
            Assert.Equal(filter.ConditionResults[0][i] && filter.ConditionResults[1][i], filter.MatchMask[i]);
        Assert.Equal(2, scene.Nodes.Count(n => n.Kind == "card"));
    }

    [Fact]
    public void JoinWithOrAndPrecedence_MatchesSqlite()
    {
        // AND binds tighter than OR: A's, plus B's from 10th graders
        const string sql = "SELECT s.name, e.grade FROM students s JOIN enrollments e ON e.student_id = s.id WHERE e.grade = 'A' OR e.grade = 'B' AND s.grade = 10;";
        var (_, scene) = Viz(sql);
        SceneAssert.ShowsQueryResult(_db, scene, sql);
    }

    [Fact]
    public void JoinWithWhereOnNullableColumn_TreatsNullAsNotMatching()
    {
        const string sql = "SELECT s.name, m.name FROM students s LEFT JOIN students m ON s.mentor_id = m.id WHERE m.major = 'Math';";
        var (result, scene) = Viz(sql);
        SceneAssert.ShowsQueryResult(_db, scene, sql);
        // NULL mentors make the comparison unknown, which WHERE rejects
        var filter = result.Filter!;
        for (int i = 0; i < result.JoinPaths.Count; i++)
            if (result.JoinPaths[i][1] == JoinChainResult.NoMatch) Assert.False(filter.MatchMask[i]);
    }

    [Fact]
    public void JoinWithWhereAndLimit_StillFiltersAllJoinedRows()
    {
        // LIMIT isn't animated for joins; the filter must still see the WHERE
        var (result, _) = Viz("SELECT s.name FROM students s JOIN enrollments e ON e.student_id = s.id WHERE e.grade = 'B' LIMIT 2;");
        Assert.Equal("e.grade = 'B'", result.Filter!.WhereText);
        Assert.Equal(4, result.Filter.MatchMask.Count(m => m));
    }

    [Fact]
    public void JoinWithInvalidWhere_ReportsTheError()
    {
        Assert.ThrowsAny<Exception>(() =>
            _db.Visualize("SELECT s.name FROM students s JOIN enrollments e ON e.student_id = s.id WHERE e.nope = 1;"));
    }

    // ── Fallback for sources without rowids ──────────────────────────────────

    [Fact]
    public void JoinOverAView_FallsBackToKeyMatching()
    {
        _db.Run("CREATE VIEW honor_roll AS SELECT student_id, grade FROM enrollments WHERE grade = 'A'");
        const string sql = "SELECT s.name, h.grade FROM students s JOIN honor_roll h ON h.student_id = s.id;";
        var (result, scene) = Viz(sql);
        Assert.Null(result.Filter);
        SceneAssert.ShowsQueryResult(_db, scene, sql);
    }
}

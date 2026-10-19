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

    // ── In-depth view: every row comparison (issue #15) ──────────────────────

    private const string InnerJoin = "SELECT s.name, e.grade FROM students s JOIN enrollments e ON e.student_id = s.id;";
    private const int Ivy = 8;   // students row with no enrollments

    private static List<VizFrame> CompareFrames(VizScene scene)
        => scene.DetailFrames!.Where(f => f.Phase!.StartsWith("Compare ")).ToList();

    [Theory]
    [InlineData(InnerJoin)]
    [InlineData(LeftJoin)]
    [InlineData("SELECT s.name, a.name FROM students s RIGHT JOIN alumni a ON s.name = a.name;")]
    [InlineData("SELECT s.name AS student_name, a.name AS alumni_name FROM students s FULL OUTER JOIN alumni a ON s.name = a.name;")]
    [InlineData("SELECT s.name AS student, m.name AS mentor FROM students s LEFT JOIN students m ON s.mentor_id = m.id;")]
    [InlineData("SELECT s.name, e.grade FROM students s LEFT JOIN enrollments e ON e.student_id = s.id AND e.grade = 'A';")]
    [InlineData("SELECT s.name FROM students s LEFT JOIN enrollments e ON e.student_id = s.id WHERE e.id IS NULL;")]
    public void InDepth_EndsWithTheSameResult(string sql)
    {
        var (_, scene) = Viz(sql);
        Assert.NotNull(scene.DetailFrames);
        Assert.Null(scene.DetailUnavailable);
        SceneAssert.ShowsQueryResult(_db, SceneAssert.InDepth(scene), sql);
    }

    [Fact]
    public void InDepth_ComparesEachLeftRowWithEveryRightRow()
    {
        var (result, scene) = Viz(InnerJoin);

        // SQLite's ON result for all 9 × 13 pairs; exactly the joined pairs are true
        var cmp = Assert.IsType<bool[][]>(result.Comparisons);
        Assert.Equal(9, cmp.Length);
        Assert.All(cmp, row => Assert.Equal(13, row.Length));
        var hits = Enumerable.Range(0, 9).SelectMany(l => Enumerable.Range(0, 13).Where(r => cmp[l][r]).Select(r => (l, r)));
        Assert.Equal(result.JoinPaths.Select(p => (p[0], p[1])).OrderBy(x => x), hits.OrderBy(x => x));

        // One step per students row, each drawing a connector to every enrollments row
        var compares = CompareFrames(scene);
        Assert.Equal(9, compares.Count);
        for (int l = 0; l < 9; l++)
        {
            var f = compares[l];
            Assert.Equal("active", SceneAssert.StateOf(f, $"t0-row-{l}"));
            Assert.Equal(13, f.ActiveEdges.Count);
            for (int r = 0; r < 13; r++)
            {
                var edge = scene.Edges[f.ActiveEdges[r]];
                Assert.Equal(($"t0-row-{l}", $"t1-row-{r}"), (edge.FromId, edge.ToId));
                Assert.Equal(cmp[l][r] ? "hit" : "miss", edge.Kind);
                Assert.Equal(cmp[l][r] ? "active" : "miss", SceneAssert.StateOf(f, $"t1-row-{r}"));
            }
        }
        Assert.StartsWith("Compare students row 1 with each enrollments row: ON is true for 2, false for 11", compares[0].Phase);

        // The normal walkthrough doesn't show the comparisons
        Assert.DoesNotContain(scene.Frames, f => f.Phase!.StartsWith("Compare "));
    }

    [Fact]
    public void InDepth_FormsEachLeftRowsMatchesRightAfterComparingIt()
    {
        var (result, scene) = Viz(InnerJoin);
        var phases = scene.DetailFrames!.Select(f => f.Phase!).ToList();
        for (int l = 0; l < 8; l++)
        {
            int at = phases.FindIndex(p => p.StartsWith($"Compare students row {l + 1} "));
            int matches = result.JoinPaths.Count(p => p[0] == l);
            // compare, then forming + emit per joined row, then the next compare
            Assert.All(phases.Skip(at + 1).Take(2 * matches), p => Assert.DoesNotContain("Compare", p));
            Assert.StartsWith("Compare ", phases[at + 1 + 2 * matches]);
        }
    }

    [Fact]
    public void InDepth_InnerJoinDropsALeftRowWithNoMatch()
    {
        var (_, scene) = Viz(InnerJoin);
        var ivy = CompareFrames(scene)[Ivy];

        Assert.EndsWith("false for all 13 — no match, so the JOIN drops this row", ivy.Phase);
        Assert.All(ivy.ActiveEdges, ei => Assert.Equal("miss", scene.Edges[ei].Kind));
        Assert.Equal("rejected", SceneAssert.StateOf(scene.DetailFrames![^1], $"t0-row-{Ivy}"));
    }

    [Fact]
    public void InDepth_LeftJoinKeepsALeftRowWithNoMatch()
    {
        var (_, scene) = Viz(LeftJoin);
        var ivy = CompareFrames(scene)[Ivy];

        Assert.EndsWith("false for all 13 — no match", ivy.Phase);
        var next = scene.DetailFrames![scene.DetailFrames.IndexOf(ivy) + 1];
        Assert.StartsWith("No match in enrollments — the outer join keeps", next.Phase);
    }

    [Fact]
    public void InDepth_NullComparisonsCountAsFalse()
    {
        // Carol has no mentor: NULL = m.id is unknown for every row, never true
        var (result, scene) = Viz("SELECT s.name AS student, m.name AS mentor FROM students s LEFT JOIN students m ON s.mentor_id = m.id;");
        Assert.All(result.Comparisons![2], Assert.False);
        Assert.StartsWith("Compare s row 3 with each m row", CompareFrames(scene)[2].Phase);
    }

    [Fact]
    public void InDepth_UnavailableWithMoreThanOneJoin()
    {
        var (result, scene) = Viz("SELECT s.name, c.title FROM students s JOIN enrollments e ON e.student_id = s.id JOIN courses c ON c.id = e.course_id;");
        Assert.Null(result.Comparisons);
        Assert.Null(scene.DetailFrames);
        Assert.Contains("single JOIN", scene.DetailUnavailable);
    }

    [Fact]
    public void InDepth_UnavailableWithoutAnOnCondition()
    {
        var (_, scene) = Viz("SELECT c.title, a.name FROM courses c CROSS JOIN alumni a;");
        Assert.Null(scene.DetailFrames);
        Assert.Contains("without an ON condition", scene.DetailUnavailable);
    }

    [Fact]
    public void InDepth_UnavailableForTooManyRowPairs()
    {
        _db.Run("CREATE TABLE big (id INTEGER PRIMARY KEY)");
        _db.Run("WITH RECURSIVE n(i) AS (SELECT 1 UNION ALL SELECT i + 1 FROM n WHERE i < 30) INSERT INTO big SELECT i FROM n");
        var (result, scene) = Viz("SELECT x.id FROM big x JOIN big y ON x.id = y.id;");
        Assert.False(result.Truncated);                   // 30 × 30 = 900 pairs, over the cap
        Assert.Null(scene.DetailFrames);
        Assert.Contains("Too many rows", scene.DetailUnavailable);
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

namespace SqlVisualizer.Tests;

/// <summary>Scan / WHERE / ORDER BY visualizations against the movies sample database.</summary>
public class VisualizerTests : IDisposable
{
    private readonly SampleDb _db = new("movies");

    public void Dispose() => _db.Dispose();

    [Theory]
    [InlineData("SELECT title, year FROM movies", "scan")]
    [InlineData("SELECT title, year FROM movies LIMIT 5", "scan")]
    [InlineData("SELECT title, rating FROM movies WHERE rating > 8.7", "where")]
    [InlineData("SELECT title, genre, year FROM movies WHERE genre = 'Crime' AND year < 1995", "where")]
    [InlineData("SELECT title, rating FROM movies ORDER BY rating DESC", "order_by")]
    [InlineData("SELECT title, rating FROM movies ORDER BY rating DESC LIMIT 3", "order_by")]
    [InlineData("SELECT title, year FROM movies WHERE genre = 'Sci-Fi' ORDER BY year", "where_order_by")]
    public void Visualization_EndsOnTheQueryResult(string sql, string vizType)
    {
        var scene = _db.Scene(sql);
        Assert.Equal(vizType, scene.VizType);
        SceneAssert.WellFormed(scene);
        SceneAssert.ShowsQueryResult(_db, scene, sql);
    }

    [Fact]
    public void Where_ShowsAConditionCardPerAndClause()
    {
        var scene = _db.Scene("SELECT title FROM movies WHERE genre = 'Crime' AND year < 1995");
        Assert.Equal(new[] { "genre = 'Crime'", "year < 1995" },
            scene.Nodes.Where(n => n.Kind == "card").Select(n => n.Badge));
    }

    [Fact]
    public void OrderBy_PlacesRowsInSortedOrder()
    {
        const string sql = "SELECT title, rating FROM movies ORDER BY rating DESC, title";
        var scene = _db.Scene(sql);
        Assert.Equal(_db.Query(sql), SceneAssert.FinalRows(scene));
    }
}

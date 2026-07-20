using System.Net.Http.Json;
using System.Text.Json;
using SqlVisualizer.Models;

namespace SqlVisualizer.Services;

/// <summary>
/// Loads Learn-section content (the lesson manifest and individual lessons) from
/// static files under wwwroot/content via HTTP. Lessons are authored as a
/// meta.json + content.md pair; the manifest lists them for the index page.
/// </summary>
public class ContentService
{
    private readonly HttpClient _http;
    private LearnManifest? _manifest;

    private static readonly JsonSerializerOptions JsonOpts =
        new(JsonSerializerDefaults.Web); // camelCase, case-insensitive

    public ContentService(HttpClient http) => _http = http;

    /// <summary>Loads (and caches) the lesson manifest.</summary>
    public async Task<LearnManifest> LoadManifestAsync()
    {
        _manifest ??= await _http.GetFromJsonAsync<LearnManifest>("content/manifest.json", JsonOpts)
                      ?? new LearnManifest(new());
        return _manifest;
    }

    /// <summary>Loads a single lesson's metadata + markdown body.</summary>
    public async Task<Lesson> LoadLessonAsync(string id)
    {
        var meta = await _http.GetFromJsonAsync<LessonMeta>($"content/lessons/{id}/meta.json", JsonOpts)
                   ?? throw new InvalidOperationException($"Lesson '{id}' has no meta.json.");
        var body = await _http.GetStringAsync($"content/lessons/{id}/content.md");
        return new Lesson(meta, body);
    }

    /// <summary>Loads a lesson's practice set, or null if it has none.</summary>
    public Task<PracticeSet?> LoadPracticeAsync(string id) =>
        LoadOptionalAsync<PracticeSet>($"content/lessons/{id}/practice.json");

    /// <summary>Loads a lesson's quiz, or null if it has none.</summary>
    public Task<Quiz?> LoadQuizAsync(string id) =>
        LoadOptionalAsync<Quiz>($"content/lessons/{id}/quiz.json");

    // A missing optional file surfaces as a 404 (or, on some static hosts, as the
    // SPA index.html fallback) — treat both as "not present".
    private async Task<T?> LoadOptionalAsync<T>(string url) where T : class
    {
        var resp = await _http.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return null;
        var mediaType = resp.Content.Headers.ContentType?.MediaType ?? "";
        if (mediaType.Contains("html")) return null;
        try { return await resp.Content.ReadFromJsonAsync<T>(JsonOpts); }
        catch (JsonException) { return null; }
    }
}

using System.Text.Json;
using Microsoft.JSInterop;

namespace SqlVisualizer.Services;

/// <summary>
/// Tracks the learner's course progress (completed lessons, solved practice
/// problems, quiz scores) and persists it to browser localStorage so it
/// survives reloads. Call <see cref="EnsureLoadedAsync"/> before reading.
/// </summary>
public class ProgressService
{
    private const string StorageKey = "sqlvis.progress.v1";

    private static readonly JsonSerializerOptions JsonOpts =
        new(JsonSerializerDefaults.Web);

    private readonly IJSRuntime _js;
    private State _state = new();
    private bool _loaded;

    /// <summary>Raised whenever progress changes, so open pages can re-render.</summary>
    public event Action? Changed;

    public ProgressService(IJSRuntime js) => _js = js;

    private sealed class State
    {
        public HashSet<string> CompletedLessons { get; set; } = new();
        public Dictionary<string, HashSet<string>> SolvedProblems { get; set; } = new();
        public Dictionary<string, QuizRecord> QuizScores { get; set; } = new();
    }

    public sealed record QuizRecord(int Correct, int Total);

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            var json = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (!string.IsNullOrEmpty(json))
                _state = JsonSerializer.Deserialize<State>(json, JsonOpts) ?? new State();
        }
        catch { /* corrupted or unavailable storage — start fresh */ }
    }

    private async Task SaveAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_state, JsonOpts);
            await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        }
        catch { /* storage unavailable (private mode etc.) — keep in-memory */ }
        Changed?.Invoke();
    }

    // ── Lessons ───────────────────────────────────────────────────────────────

    public bool IsLessonComplete(string lessonId) =>
        _state.CompletedLessons.Contains(lessonId);

    public async Task MarkLessonCompleteAsync(string lessonId)
    {
        if (_state.CompletedLessons.Add(lessonId))
            await SaveAsync();
    }

    // ── Practice problems ─────────────────────────────────────────────────────

    public bool IsSolved(string lessonId, string problemId) =>
        _state.SolvedProblems.TryGetValue(lessonId, out var set) && set.Contains(problemId);

    public int SolvedCount(string lessonId) =>
        _state.SolvedProblems.TryGetValue(lessonId, out var set) ? set.Count : 0;

    public async Task MarkSolvedAsync(string lessonId, string problemId)
    {
        if (!_state.SolvedProblems.TryGetValue(lessonId, out var set))
            _state.SolvedProblems[lessonId] = set = new HashSet<string>();
        if (set.Add(problemId))
            await SaveAsync();
    }

    // ── Quiz ──────────────────────────────────────────────────────────────────

    public QuizRecord? QuizScore(string lessonId) =>
        _state.QuizScores.TryGetValue(lessonId, out var r) ? r : null;

    public async Task RecordQuizScoreAsync(string lessonId, int correct, int total)
    {
        // Keep the learner's best attempt.
        var prev = QuizScore(lessonId);
        if (prev == null || correct > prev.Correct)
        {
            _state.QuizScores[lessonId] = new QuizRecord(correct, total);
            await SaveAsync();
        }
    }

    // ── Reset ─────────────────────────────────────────────────────────────────

    public async Task ResetAsync()
    {
        _state = new State();
        await SaveAsync();
    }
}

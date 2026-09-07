using System.Text.Json;
using Microsoft.JSInterop;

namespace SqlVisualizer.Services;

/// <summary>
/// Remembers whether the user has been through (or dismissed) the Playground
/// walkthrough, persisted to browser localStorage so it only shows once.
/// Call <see cref="EnsureLoadedAsync"/> before reading <see cref="HasSeen"/>.
/// </summary>
public class OnboardingService
{
    private const string StorageKey = "sqlvis.onboarding.v1";

    private static readonly JsonSerializerOptions JsonOpts =
        new(JsonSerializerDefaults.Web);

    private readonly IJSRuntime _js;
    private State? _state;
    private bool _loaded;

    public OnboardingService(IJSRuntime js) => _js = js;

    private sealed class State
    {
        /// <summary>"completed" or "skipped".</summary>
        public string Status { get; set; } = "";
        public DateTime At { get; set; }
        /// <summary>Zero-based step index reached when the tour was dismissed.</summary>
        public int Step { get; set; }
    }

    /// <summary>True once the user has either finished or skipped the tour.</summary>
    public bool HasSeen => _state != null;

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            var json = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (!string.IsNullOrEmpty(json))
                _state = JsonSerializer.Deserialize<State>(json, JsonOpts);
        }
        catch { /* corrupted or unavailable storage — treat as unseen */ }
    }

    public Task MarkCompletedAsync(int step) => SaveAsync("completed", step);
    public Task MarkSkippedAsync(int step)   => SaveAsync("skipped", step);

    /// <summary>Forget the stored state so the tour shows again.</summary>
    public async Task ResetAsync()
    {
        _state = null;
        try { await _js.InvokeVoidAsync("localStorage.removeItem", StorageKey); }
        catch { }
    }

    private async Task SaveAsync(string status, int step)
    {
        _state = new State { Status = status, At = DateTime.UtcNow, Step = step };
        try
        {
            var json = JsonSerializer.Serialize(_state, JsonOpts);
            await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        }
        catch { /* storage unavailable (private mode etc.) — keep in-memory */ }
    }
}

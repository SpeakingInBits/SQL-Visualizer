using Microsoft.JSInterop;
using SqlVisualizer.Models;
using System.Text.Json;

namespace SqlVisualizer.Services;

/// <summary>
/// Persists SQL scripts to browser localStorage via IJSRuntime (no external package required).
/// </summary>
public class ScriptStoreService
{
    private const string IndexKey = "sql_visualizer_scripts";
    private readonly IJSRuntime _js;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public ScriptStoreService(IJSRuntime js) => _js = js;

    public async Task<List<Script>> ListScriptsAsync()
    {
        var raw = await _js.InvokeAsync<string?>("localStorage.getItem", IndexKey);
        if (string.IsNullOrEmpty(raw)) return [];
        return JsonSerializer.Deserialize<List<Script>>(raw, _json) ?? [];
    }

    public async Task<Script> SaveScriptAsync(string name, string content, string engineHint = "sqlite")
    {
        var list = await ListScriptsAsync();
        var script = new Script(
            Id:         Guid.NewGuid().ToString(),
            Name:       Path.GetFileName(name),
            EngineHint: engineHint,
            Content:    content,
            CreatedAt:  DateTime.UtcNow,
            LastUsed:   null);
        list.Add(script);
        await SaveList(list);
        return script;
    }

    public async Task<bool> DeleteScriptAsync(string id)
    {
        var list = await ListScriptsAsync();
        var idx = list.FindIndex(s => s.Id == id);
        if (idx < 0) return false;
        list.RemoveAt(idx);
        await SaveList(list);
        return true;
    }

    public async Task<Script?> GetScriptAsync(string id)
    {
        var list = await ListScriptsAsync();
        return list.FirstOrDefault(s => s.Id == id);
    }

    public async Task TouchScriptAsync(string id)
    {
        var list = await ListScriptsAsync();
        var idx = list.FindIndex(s => s.Id == id);
        if (idx < 0) return;
        list[idx] = list[idx] with { LastUsed = DateTime.UtcNow };
        await SaveList(list);
    }

    private async Task SaveList(List<Script> list)
    {
        var json = JsonSerializer.Serialize(list, _json);
        await _js.InvokeVoidAsync("localStorage.setItem", IndexKey, json);
    }
}

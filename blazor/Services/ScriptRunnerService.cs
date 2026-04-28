using SqlVisualizer.Models;
using System.Text.RegularExpressions;

namespace SqlVisualizer.Services;

/// <summary>
/// Splits and executes multi-statement SQL scripts (mirrors Python routes/scripts.py logic).
/// </summary>
public class ScriptRunnerService
{
    private readonly QueryExecutorService _executor;

    public ScriptRunnerService(QueryExecutorService executor) => _executor = executor;

    public ScriptRunResult Run(Script script)
    {
        // Split on semicolons (SQLite convention); skip empty chunks
        var statements = script.Content
            .Split(';')
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();

        var results = new List<StatementResult>();
        foreach (var stmt in statements)
        {
            try
            {
                var r = _executor.RunStatement(stmt);
                results.Add(new StatementResult(
                    Statement:    Truncate(stmt, 120),
                    Type:         r.Type,
                    RowsAffected: r.RowsAffected,
                    Columns:      r.Columns,
                    Rows:         r.Rows,
                    Error:        null,
                    Ok:           true));
            }
            catch (Exception ex)
            {
                results.Add(new StatementResult(
                    Statement:    Truncate(stmt, 120),
                    Type:         "ERROR",
                    RowsAffected: null,
                    Columns:      null,
                    Rows:         null,
                    Error:        ex.Message,
                    Ok:           false));
            }
        }

        return new ScriptRunResult(script.Id, results);
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max] + "…";
}

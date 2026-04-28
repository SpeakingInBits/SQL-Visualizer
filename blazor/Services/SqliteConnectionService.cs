using Microsoft.Data.Sqlite;
using SqlVisualizer.Models;

namespace SqlVisualizer.Services;

/// <summary>
/// Manages a single active SQLite connection (mirrors Python connector.py).
/// In WASM the database bytes are loaded via JS interop and written to the
/// in-process virtual file system so that Microsoft.Data.Sqlite can open them.
/// </summary>
public class SqliteConnectionService : IDisposable
{
    private SqliteConnection? _conn;
    private ConnectionStatus _status = new(false);

    public ConnectionStatus Status => _status;

    // ── Open from raw bytes (file picker upload) ─────────────────────────────

    public void OpenFromBytes(byte[] dbBytes, string displayName)
    {
        Close();
        // Write bytes to an in-memory temp path accessible within WASM process
        var path = Path.Combine(Path.GetTempPath(), $"sqlvis_{Guid.NewGuid():N}.db");
        File.WriteAllBytes(path, dbBytes);
        _conn = new SqliteConnection($"Data Source={path}");
        _conn.Open();
        _status = new ConnectionStatus(true, displayName);
    }

    // ── Open a sample DB generated in-memory ─────────────────────────────────

    public void OpenInMemory(string displayName, Action<SqliteConnection> seed)
    {
        Close();
        // Use a named in-memory DB so it persists for the connection lifetime
        var name = $"sqlvis_{Guid.NewGuid():N}";
        _conn = new SqliteConnection($"Data Source={name};Mode=Memory;Cache=Shared");
        _conn.Open();
        seed(_conn);
        _status = new ConnectionStatus(true, displayName);
    }

    // ── Disconnect ────────────────────────────────────────────────────────────

    public void Close()
    {
        if (_conn is not null)
        {
            _conn.Close();
            _conn.Dispose();
            _conn = null;
        }
        _status = new ConnectionStatus(false);
    }

    // ── Require active (throws if not connected) ──────────────────────────────

    public SqliteConnection Require()
    {
        if (_conn is null || _conn.State != System.Data.ConnectionState.Open)
            throw new InvalidOperationException("No active connection.");
        return _conn;
    }

    public void Dispose() => Close();
}

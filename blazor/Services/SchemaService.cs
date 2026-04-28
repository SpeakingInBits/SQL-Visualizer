using Microsoft.Data.Sqlite;
using SqlVisualizer.Models;

namespace SqlVisualizer.Services;

/// <summary>
/// Schema introspection using SQLite PRAGMA commands (mirrors Python schema.py).
/// </summary>
public class SchemaService
{
    private readonly SqliteConnectionService _conn;

    public SchemaService(SqliteConnectionService conn) => _conn = conn;

    public List<TableRef> ListTables()
    {
        var c = _conn.Require();
        var tables = new List<TableRef>();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            tables.Add(new TableRef("main", reader.GetString(0)));
        return tables;
    }

    public ColumnsResponse GetColumns(string table)
    {
        var c = _conn.Require();

        // Get columns via PRAGMA table_info
        var columns = new List<ColumnMeta>();
        using var colCmd = c.CreateCommand();
        colCmd.CommandText = $"PRAGMA table_info(\"{table.Replace("\"", "\"\"")}\");";
        using (var r = colCmd.ExecuteReader())
        {
            while (r.Read())
            {
                columns.Add(new ColumnMeta(
                    Name:         r.GetString(r.GetOrdinal("name")),
                    DataType:     r.GetString(r.GetOrdinal("type")),
                    IsNullable:   r.GetInt32(r.GetOrdinal("notnull")) == 0,
                    IsPrimaryKey: r.GetInt32(r.GetOrdinal("pk")) != 0,
                    IsForeignKey: false,    // filled in below
                    Default:      r.IsDBNull(r.GetOrdinal("dflt_value")) ? null : r.GetString(r.GetOrdinal("dflt_value"))
                ));
            }
        }

        // Get foreign keys via PRAGMA foreign_key_list
        var fks = new List<ForeignKey>();
        using var fkCmd = c.CreateCommand();
        fkCmd.CommandText = $"PRAGMA foreign_key_list(\"{table.Replace("\"", "\"\"")}\");";
        using (var r = fkCmd.ExecuteReader())
        {
            while (r.Read())
            {
                fks.Add(new ForeignKey(
                    Column:    r.GetString(r.GetOrdinal("from")),
                    RefTable:  r.GetString(r.GetOrdinal("table")),
                    RefColumn: r.GetString(r.GetOrdinal("to"))
                ));
            }
        }

        // Mark FK columns
        var fkNames = fks.Select(f => f.Column).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var enriched = columns.Select(col =>
            col with { IsForeignKey = fkNames.Contains(col.Name) }).ToList();

        return new ColumnsResponse(enriched, fks);
    }
}

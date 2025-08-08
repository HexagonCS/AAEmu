using AAEmu.Commons.IO;
using Microsoft.Data.Sqlite;
using NLog;

namespace AAEmu.Game.Utils.DB;

/// <summary>
/// Helper for inspecting the compact.sqlite3 game data file.
/// Provides methods to verify presence, list tables, columns, indices, and row counts.
/// </summary>
public static class CompactSqliteHelper
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public static string GetDatabasePath(string directory = "Data", string sqlite = "compact.sqlite3")
    {
        return Path.Combine(FileManager.AppPath, directory, sqlite);
    }

    public static bool Exists(string? path = null)
    {
        path ??= GetDatabasePath();
        return File.Exists(path);
    }

    public static IEnumerable<string> GetTables(string? path = null)
    {
        using var conn = SQLite.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            yield return reader.GetString(0);
        }
    }

    public static IEnumerable<ColumnInfo> GetTableColumns(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            yield break;

        using var conn = SQLite.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({QuoteIdent(tableName)});";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            // cid, name, type, notnull, dflt_value, pk
            var cid = reader.GetInt32(0);
            var name = reader.GetString(1);
            var type = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            var notNull = reader.GetInt32(3) == 1;
            var defaultValue = reader.IsDBNull(4) ? null : reader.GetValue(4)?.ToString();
            var isPk = reader.GetInt32(5) == 1;
            yield return new ColumnInfo(cid, name, type, notNull, isPk, defaultValue);
        }
    }

    public static long GetRowCount(string tableName)
    {
        using var conn = SQLite.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(1) FROM {QuoteIdent(tableName)};";
        var result = cmd.ExecuteScalar();
        return result is long l ? l : Convert.ToInt64(result);
    }

    public static IEnumerable<IndexInfo> GetIndices(string tableName)
    {
        using var conn = SQLite.CreateConnection();
        using var listCmd = conn.CreateCommand();
        listCmd.CommandText = $"PRAGMA index_list({QuoteIdent(tableName)});";
        using var listReader = listCmd.ExecuteReader();
        while (listReader.Read())
        {
            var name = listReader.GetString(1);
            var isUnique = listReader.GetInt32(2) == 1;

            var columns = new List<string>();
            using var infoCmd = conn.CreateCommand();
            infoCmd.CommandText = $"PRAGMA index_info({QuoteIdent(name)});";
            using var infoReader = infoCmd.ExecuteReader();
            while (infoReader.Read())
            {
                columns.Add(infoReader.GetString(2));
            }

            yield return new IndexInfo(name, isUnique, columns);
        }
    }

    public static IEnumerable<TableSummary> GetSchemaSummary()
    {
        foreach (var table in GetTables())
        {
            var columns = GetTableColumns(table).ToList();
            long rows;
            try
            {
                rows = GetRowCount(table);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to count rows for {Table}", table);
                rows = -1;
            }
            yield return new TableSummary(table, columns.Count, rows);
        }
    }

    public static void LogSchemaOverview()
    {
        var path = GetDatabasePath();
        if (!Exists(path))
        {
            Logger.Error("compact.sqlite3 not found at {Path}", path);
            return;
        }

        Logger.Info("Inspecting compact.sqlite3 at {Path}", path);
        foreach (var summary in GetSchemaSummary())
        {
            Logger.Info("Table {Table} | columns: {Columns} | rows: {Rows}", summary.Table, summary.ColumnCount, summary.RowCount);
        }
    }

    private static string QuoteIdent(string ident)
    {
        // Minimal quoting to mitigate injection in identifiers
        return "\"" + ident.Replace("\"", "\"\"") + "\"";
    }

    public readonly record struct ColumnInfo(int Ordinal, string Name, string Type, bool NotNull, bool PrimaryKey, string? DefaultValue);

    public readonly record struct IndexInfo(string Name, bool Unique, IReadOnlyList<string> Columns);

    public readonly record struct TableSummary(string Table, int ColumnCount, long RowCount);
}


using System.Text;
using System.Text.Json;
using AAEmu.Commons.IO;
using Microsoft.Data.Sqlite;
using NLog;

#nullable enable

namespace AAEmu.Game.Utils.DB;

public static class SchemaInspector
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public sealed class Table
    {
        public string Name { get; set; } = string.Empty;
        public List<CompactSqliteHelper.ColumnInfo> Columns { get; set; } = new();
        public long RowCount { get; set; }
    }

    public sealed class Edge
    {
        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
        public string ViaColumn { get; set; } = string.Empty;
        public string? Through { get; set; } // mapping table if applicable
        public string Kind { get; set; } = "fk_like"; // fk_like | mapping
    }

    public sealed class Snapshot
    {
        public List<Table> Tables { get; set; } = new();
        public List<Edge> Edges { get; set; } = new();
        public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
        public string DatabasePath { get; set; } = CompactSqliteHelper.GetDatabasePath();
    }

    public static Snapshot Capture()
    {
        var snapshot = new Snapshot();
        var tables = CompactSqliteHelper.GetTables().ToList();
        var tableSet = new HashSet<string>(tables, StringComparer.OrdinalIgnoreCase);

        foreach (var t in tables)
        {
            var cols = CompactSqliteHelper.GetTableColumns(t).ToList();
            long rows;
            try { rows = CompactSqliteHelper.GetRowCount(t); }
            catch { rows = -1; }

            snapshot.Tables.Add(new Table
            {
                Name = t,
                Columns = cols,
                RowCount = rows
            });
        }

        // Infer relationships by *_id columns
        using var conn = SQLite.CreateConnection();
        foreach (var table in snapshot.Tables)
        {
            var idCols = table.Columns.Where(c => c.Name.EndsWith("_id", StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var col in idCols)
            {
                var candidates = GuessTargetTables(col.Name, tableSet);
                var verified = VerifyCandidates(conn, table.Name, col.Name, candidates);
                foreach (var v in verified)
                {
                    snapshot.Edges.Add(new Edge
                    {
                        From = table.Name,
                        To = v,
                        ViaColumn = col.Name,
                        Kind = "fk_like"
                    });
                }
            }
        }

        // Mapping tables: have two or more *_id columns and a plural name like *_something_s
        foreach (var table in snapshot.Tables)
        {
            var idCols = table.Columns.Where(c => c.Name.EndsWith("_id", StringComparison.OrdinalIgnoreCase)).ToList();
            if (idCols.Count >= 2)
            {
                foreach (var (a, b) in Pairwise(idCols))
                {
                    // Try to connect A -> B through mapping table
                    var aTargets = GuessTargetTables(a.Name, tableSet);
                    var bTargets = GuessTargetTables(b.Name, tableSet);

                    foreach (var at in aTargets)
                    foreach (var bt in bTargets)
                    {
                        snapshot.Edges.Add(new Edge
                        {
                            From = at,
                            To = bt,
                            ViaColumn = a.Name + "," + b.Name,
                            Through = table.Name,
                            Kind = "mapping"
                        });
                    }
                }
            }
        }

        return snapshot;
    }

    public static void WriteJson(Snapshot snapshot, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var path = Path.Combine(outDir, "schema_snapshot.json");
        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
        Logger.Info("Wrote {Path}", path);
    }

    public static void WriteDot(Snapshot snapshot, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var sb = new StringBuilder();
        sb.AppendLine("digraph compact {\n  graph [rankdir=LR];\n  node [shape=box, fontsize=10];");

        foreach (var t in snapshot.Tables.OrderBy(t => t.Name))
        {
            sb.AppendLine($"  \"{t.Name}\" [label=\"{t.Name} ({t.RowCount})\"]; ");
        }

        foreach (var e in snapshot.Edges)
        {
            var style = e.Kind == "mapping" ? " [style=dashed, label=\"" + (e.Through ?? e.ViaColumn) + "\"]" : " [label=\"" + e.ViaColumn + "\"]";
            sb.AppendLine($"  \"{e.From}\" -> \"{e.To}\"{style};");
        }

        sb.AppendLine("}");
        var path = Path.Combine(outDir, "schema_graph.dot");
        File.WriteAllText(path, sb.ToString());
        Logger.Info("Wrote {Path}", path);
    }

    public static void WriteMarkdown(Snapshot snapshot, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var path = Path.Combine(outDir, "README.md");
        var sb = new StringBuilder();
        sb.AppendLine("# compact.sqlite3 Inspection\n");
        sb.AppendLine($"Generated: {DateTime.UtcNow:u}\n");
        sb.AppendLine("## Overview\n");
        sb.AppendLine($"- Tables: {snapshot.Tables.Count}\n- Edges (inferred): {snapshot.Edges.Count}\n- DB Path: `{snapshot.DatabasePath}`\n");

        sb.AppendLine("## Top Tables by Rows\n");
        foreach (var t in snapshot.Tables.Where(t => t.RowCount >= 0).OrderByDescending(t => t.RowCount).Take(20))
        {
            sb.AppendLine($"- {t.Name}: {t.RowCount}");
        }

        sb.AppendLine("\n## Frequent Reference Columns\n");
        var refCounts = snapshot.Tables
            .SelectMany(t => t.Columns)
            .Where(c => c.Name.EndsWith("_id", StringComparison.OrdinalIgnoreCase))
            .GroupBy(c => c.Name)
            .OrderByDescending(g => g.Count())
            .Take(20);
        foreach (var g in refCounts)
            sb.AppendLine($"- {g.Key}: appears in {g.Count()} tables");

        sb.AppendLine("\n## Sample Relationships\n");
        foreach (var e in snapshot.Edges.Take(50))
            sb.AppendLine($"- {e.From} -> {e.To} via `{e.ViaColumn}`{(e.Through != null ? $" (through {e.Through})" : string.Empty)}");

        File.WriteAllText(path, sb.ToString());
        Logger.Info("Wrote {Path}", path);
    }

    public static void GenerateAll(string outDir)
    {
        var snapshot = Capture();
        WriteJson(snapshot, outDir);
        WriteDot(snapshot, outDir);
        WriteMarkdown(snapshot, outDir);
    }

    private static IEnumerable<(T left, T right)> Pairwise<T>(List<T> items)
    {
        for (int i = 0; i < items.Count; i++)
            for (int j = i + 1; j < items.Count; j++)
                yield return (items[i], items[j]);
    }

    private static IEnumerable<string> GuessTargetTables(string columnName, HashSet<string> tableSet)
    {
        var baseName = columnName[..^3]; // strip _id
        var cands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            baseName,
            baseName + "s",
            baseName + "es",
        };

        if (baseName.EndsWith("y", StringComparison.OrdinalIgnoreCase))
            cands.Add(baseName[..^1] + "ies");

        // existing only
        var existing = cands.Where(t => tableSet.Contains(t)).ToList();
        if (existing.Count > 0)
            return existing;

        // fallback: fuzzy contains
        var fuzzy = tableSet.Where(t => t.Contains(baseName, StringComparison.OrdinalIgnoreCase)).ToList();
        return fuzzy.Count > 0 ? fuzzy : Array.Empty<string>();
    }

    private static List<string> VerifyCandidates(SqliteConnection conn, string table, string column, IEnumerable<string> candidates)
    {
        var list = candidates.ToList();
        if (list.Count == 0)
            return list;

        // Collect sample of non-null IDs
        var ids = new List<long>();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT {column} FROM \"{table}\" WHERE {column} IS NOT NULL LIMIT 25;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (!reader.IsDBNull(0))
                {
                    var val = reader.GetValue(0);
                    if (val is long l) ids.Add(l);
                    else if (val is int i) ids.Add(i);
                    else if (long.TryParse(val.ToString(), out var p)) ids.Add(p);
                }
            }
        }
        catch
        {
            return list; // return unverified
        }

        if (ids.Count == 0)
            return list;

        var verified = new List<string>();
        foreach (var cand in list)
        {
            try
            {
                var values = string.Join(",", ids.Take(25));
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"SELECT 1 FROM \"{cand}\" WHERE id IN ({values}) LIMIT 1;";
                var result = cmd.ExecuteScalar();
                if (result != null)
                    verified.Add(cand);
            }
            catch
            {
                // ignore
            }
        }

        return verified.Count > 0 ? verified : list;
    }
}

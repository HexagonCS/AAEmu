using System.Linq;
using AAEmu.Game.Utils.DB;
using Xunit;

namespace AAEmu.UnitTests.Utils.Db;

public class CompactSqliteHelperTests
{
    [Fact]
    public void Can_List_Tables_And_Schema()
    {
        Assert.True(CompactSqliteHelper.Exists(), "compact.sqlite3 is missing under Data/");

        var tables = CompactSqliteHelper.GetTables().ToList();
        Assert.NotEmpty(tables);

        // Print a concise overview to test output
        foreach (var table in tables.Take(10)) // limit output
        {
            var cols = CompactSqliteHelper.GetTableColumns(table).ToList();
            var count = CompactSqliteHelper.GetRowCount(table);
            System.Console.WriteLine($"{table}: {cols.Count} cols, {count} rows");
        }
    }
}


using System.Data;
using Loader.Core.Providers.ClickHouse;
using Loader.Core.Providers.Sql;
using Loader.Core.Sources;
using Loader.Core.Tests.Infrastructure;
using Loader.Core.Writers.ClickHouse;

namespace Loader.Core.Tests;

public sealed class ClickHouseWriterTests
{
    private static ClickHouseTestDatabase? Database;

    [Before(Class)]
    public static async Task StartDatabase()
    {
        Database = await ClickHouseTestDatabase.StartAsync();
    }

    [After(Class)]
    public static async Task StopDatabase()
    {
        if (Database is not null)
        {
            await Database.DisposeAsync();
        }
    }

    [Test]
    [DisplayName("ClickHouseWriter создает таблицу по meta и пишет reader через bulk copy")]
    public async Task Creates_table_from_meta_and_writes_reader_with_bulk_copy()
    {
        using var analyzeTable = CreateTable();
        analyzeTable.Rows.Add(1, 10.50m, "Moscow", true);
        analyzeTable.Rows.Add(2, DBNull.Value, "London", false);
        analyzeTable.Rows.Add(3, 20.25m, "Moscow", true);
        var meta = new DataMetaContainer();

        using (var rawAnalyzeReader = analyzeTable.CreateDataReader())
        await using (var analyzeReader = rawAnalyzeReader.Normalize().CollectMeta(meta))
        {
            while (await analyzeReader.ReadAsync())
            {
            }
        }

        using var writeTable = CreateTable();
        writeTable.Rows.Add(1, 10.50m, "Moscow", true);
        writeTable.Rows.Add(2, DBNull.Value, "London", false);
        writeTable.Rows.Add(3, 20.25m, "Moscow", true);

        using var rawWriteReader = writeTable.CreateDataReader();
        await using var writeReader = rawWriteReader.Normalize();
        var writer = new ClickHouseWriter();
        var tableName = "writer_meta_" + Guid.NewGuid().ToString("N");

        await writer.WriteAsync(
            Source(),
            writeReader,
            new ClickHouseWriteOptions
            {
                TableName = new ClickHouseTableName
                {
                    Table = tableName
                }
            },
            meta);

        await using var rawResultReader = await new ClickHouseProvider().OpenReaderAsync(
            Source(),
            new SqlTableConfig
            {
                Sql = $"""
                      select id, amount, city, active
                      from {tableName}
                      order by id
                      """
            });
        await using var resultReader = rawResultReader.Normalize();

        await Assert.That(resultReader).HaveData(
            columns: ["id", "amount", "city", "active"],
            types: [DataType.Integer, DataType.Number, DataType.Text, DataType.Boolean],
            rows: [
                ((byte)1, 10.50m, "Moscow", true),
                ((byte)2, DBNull.Value, "London", false),
                ((byte)3, 20.25m, "Moscow", true)
            ]);
    }

    [Test]
    [DisplayName("ClickHouseWriter BuildCreateTableSql сужает integer и decimal по meta")]
    public async Task Build_create_table_sql_uses_meta_for_integer_and_decimal_types()
    {
        using var table = CreateTable();
        table.Rows.Add(1, 10.50m, "Moscow", true);
        table.Rows.Add(2, DBNull.Value, "London", false);
        var meta = new DataMetaContainer();

        using (var rawAnalyzeReader = table.CreateDataReader())
        await using (var analyzeReader = rawAnalyzeReader.Normalize().CollectMeta(meta))
        {
            while (await analyzeReader.ReadAsync())
            {
            }
        }

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader.Normalize();
        var sql = new ClickHouseWriter().BuildCreateTableSql(
            reader,
            new ClickHouseWriteOptions
            {
                TableName = new ClickHouseTableName
                {
                    Table = "target_table"
                }
            },
            meta);

        await Assert.That(sql).Contains("`id` UInt8");
        await Assert.That(sql).Contains("`amount` Nullable(Decimal(");
        await Assert.That(sql).Contains("`city` LowCardinality(String)");
        await Assert.That(sql).Contains("ENGINE = Log");
    }

    private static DataTable CreateTable()
    {
        var table = new DataTable();
        table.Columns.Add("id", typeof(int));
        table.Columns.Add("amount", typeof(decimal));
        table.Columns.Add("city", typeof(string));
        table.Columns.Add("active", typeof(bool));
        return table;
    }

    private static ConnectionStringSource Source()
    {
        var database = Database ?? throw new InvalidOperationException("ClickHouse test database is not started.");
        return new ConnectionStringSource
        {
            ConnectionString = database.ConnectionString
        };
    }
}

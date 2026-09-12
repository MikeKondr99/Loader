using System.Data;
using System.Diagnostics;
using ClickHouse.Client.Numerics;
using Loader.Core.Providers.ClickHouse;
using Loader.Core.Providers.Sql;
using Loader.Core.Sources;
using Loader.Core.Tests.Infrastructure;
using Loader.Core.Writers.ClickHouse;

namespace Loader.Core.Tests;

[TestWithDependency(DatabaseDependency.ClickHouse)]
public sealed class ClickHouseWriterTests
{
    private readonly ClickHouseTestDatabase database;

    public ClickHouseWriterTests(ClickHouseTestDatabase database)
    {
        this.database = database;
    }

    [Test]
    [DisplayName("ClickHouseWriter создает таблицу по schema и пишет reader через bulk copy")]
    public async Task Creates_table_from_schema_and_writes_reader_with_bulk_copy()
    {
        using var writeTable = CreateTable();
        writeTable.Rows.Add(1, 10.50m, "Moscow", true);
        writeTable.Rows.Add(2, DBNull.Value, "London", false);
        writeTable.Rows.Add(3, 20.25m, "Moscow", true);

        using var rawWriteReader = writeTable.CreateDataReader();
        await using var writeReader = rawWriteReader.Normalize();
        var writer = new ClickHouseWriter();
        var tableName = "writer_schema_" + Guid.NewGuid().ToString("N");

        await writer.WriteAsync(
            Source(),
            writeReader,
            new ClickHouseWriteOptions
            {
                TableName = new ClickHouseTableName
                {
                    Table = tableName
                }
            });

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
                (1, (ClickHouseDecimal)10.50m, "Moscow", true),
                (2, DBNull.Value, "London", false),
                (3, (ClickHouseDecimal)20.25m, "Moscow", true)
            ]);
    }

    [Test]
    [DisplayName("ClickHouseWriter добавляет telemetry tags с CREATE TABLE и INSERT sql")]
    public async Task Write_adds_current_activity_sql_tags()
    {
        using var table = CreateTable();
        table.Rows.Add(1, 10.50m, "Moscow", true);

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader.Normalize();

        using var activity = new Activity("test").Start();
        var tableName = "writer_telemetry_" + Guid.NewGuid().ToString("N");

        await new ClickHouseWriter().WriteAsync(
            Source(),
            reader,
            new ClickHouseWriteOptions
            {
                TableName = new ClickHouseTableName
                {
                    Table = tableName
                }
            });

        var tags = activity.TagObjects.ToDictionary(
            static tag => tag.Key,
            static tag => tag.Value);

        await Assert.That(tags["db.system"]).IsEqualTo("clickhouse");
        await Assert.That(tags["db.statement.create_table"]?.ToString()).Contains($"`{tableName}`");
        await Assert.That(tags["db.statement.create_table"]?.ToString()).Contains("CREATE TABLE");
        await Assert.That(tags["db.statement.insert"]?.ToString()).IsEqualTo(
            $"INSERT INTO `{tableName}` (" + Environment.NewLine +
            "    `id`," + Environment.NewLine +
            "    `amount`," + Environment.NewLine +
            "    `city`," + Environment.NewLine +
            "    `active`" + Environment.NewLine +
            ")");
    }

    [Test]
    [DisplayName("ClickHouseWriter BuildCreateTableSql строит типы по schema")]
    public async Task Build_create_table_sql_uses_schema_types()
    {
        using var table = CreateTable();
        table.Rows.Add(1, 10.50m, "Moscow", true);
        table.Rows.Add(2, DBNull.Value, "London", false);

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
            });

        await Assert.That(sql).Contains("`id` Nullable(Int32)");
        await Assert.That(sql).Contains("`amount` Nullable(Decimal(");
        await Assert.That(sql).Contains("`city` Nullable(String)");
        await Assert.That(sql).Contains("ENGINE = MergeTree ORDER BY tuple()");
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

    private ConnectionStringSource Source()
    {
        return new ConnectionStringSource
        {
            ConnectionString = database.ConnectionString
        };
    }
}

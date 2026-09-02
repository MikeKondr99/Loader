using Loader.Script.Tests.Infrastructure;
using Microsoft.Data.SqlClient;

namespace Loader.Script.Tests;

[TestWithDependency(DatabaseDependency.ClickHouseDwh, DatabaseDependency.SqlServer)]
public sealed class LoadStatementSqlServerTests
{
    private readonly ClickHouseTestDatabase clickHouse;
    private readonly SqlServerTestDatabase sqlServer;

    public LoadStatementSqlServerTests(ClickHouseTestDatabase clickHouse, SqlServerTestDatabase sqlServer)
    {
        this.clickHouse = clickHouse;
        this.sqlServer = sqlServer;
    }

    [Test]
    [DisplayName("LOAD из Connect SqlServer source перегружает данные через temp в final table")]
    public async Task SqlServer_load_materializes_expected_final_table()
    {
        // Arrange
        var sourceTable = $"script_sql_source_{Guid.NewGuid():N}";
        await ExecuteSqlServerAsync(
            sqlServer,
            $$"""
            CREATE TABLE dbo.{{sourceTable}}
            (
                id int not null,
                name nvarchar(100) not null,
                city nvarchar(100) not null,
                active bit not null,
                amount decimal(10, 2) null,
                created_at datetime2 not null,
                note nvarchar(100) null
            );
            INSERT INTO dbo.{{sourceTable}} (id, name, city, active, amount, created_at, note) VALUES
            (1, N'Alice', N'Moscow', 1, 10.50, '2024-01-01T10:11:12', N'vip'),
            (2, N'Bob', N'Berlin', 0, NULL, '2024-01-02T11:12:13', NULL),
            (3, N'Charlie', N'London', 1, 25.75, '2024-01-03T12:13:14', N'new');
            """);

        // Act
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            clickHouse,
            $$"""
            sql_people:
            LOAD
                Text(id) AS id,
                Upper(name) AS name,
                city AS Город,
                active,
                amount,
                created_at,
                note
            FROM Connect(name='container_mssql')
            SQL SELECT * FROM dbo.{{sourceTable}} WHERE city != 'Berlin' ORDER BY id ASC;

            sql_people_copy:
            LOAD *
            FROM sql_people
            ORDER BY id ASC;
            """,
            sqlServer);

        // Assert
        var result = execution.Tables;
        await Assert.That(result).Count().IsEqualTo(2);
        await Assert.That(result[0].Alias).IsEqualTo("sql_people");
        await ScriptIntegrationAssert.AssertFinalTableAsync(
            clickHouse,
            result[1],
            ["id", "name", "Город", "active", "amount", "created_at", "note"],
            [
                ["1", "ALICE", "Moscow", true, 10.50m, new DateTime(2024, 1, 1, 10, 11, 12), "vip"],
                ["3", "CHARLIE", "London", true, 25.75m, new DateTime(2024, 1, 3, 12, 13, 14), "new"]
            ],
            "ORDER BY `column1` ASC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(clickHouse, execution);
    }

    [Test]
    [DisplayName("LOAD из Connect SqlServer source материализует bigint")]
    public async Task SqlServer_load_materializes_bigint()
    {
        var sourceTable = $"script_sql_bigint_source_{Guid.NewGuid():N}";
        await ExecuteSqlServerAsync(
            sqlServer,
            $$"""
            CREATE TABLE dbo.{{sourceTable}}
            (
                id bigint not null
            );
            INSERT INTO dbo.{{sourceTable}} (id) VALUES
            (9223372036854775807),
            (-9223372036854775808);
            """);

        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            clickHouse,
            $$"""
            sql_bigint:
            LOAD *
            FROM Connect(name='container_mssql')
            SQL SELECT * FROM dbo.{{sourceTable}} ORDER BY id DESC;
            """,
            sqlServer);

        var result = execution.Tables;
        await Assert.That(result).Count().IsEqualTo(1);
        await ScriptIntegrationAssert.AssertFinalTableAsync(
            clickHouse,
            result[0],
            ["id"],
            [
                [long.MaxValue],
                [long.MinValue]
            ],
            "ORDER BY `column1` DESC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(clickHouse, execution);
    }

    [Test]
    [DisplayName("LOAD из результата SqlServer LOAD сохраняет time как Text")]
    public async Task SqlServer_load_from_previous_load_preserves_time_as_text()
    {
        // Arrange
        // Act
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            clickHouse,
            $$"""
            sql_time:
            LOAD
              id,
              time_value
            FROM Connect(name='container_mssql')
            SQL
              SELECT
                1 AS id,
                cast('03:04:05' as time) AS time_value;

            s:
            LOAD * FROM sql_time;
            """,
            sqlServer);

        // Assert
        var result = execution.Tables;
        await Assert.That(result).Count().IsEqualTo(2);
        await Assert.That(result[0].Fields[1].DataType).IsEqualTo(Loader.Core.Models.DataType.Text);
        await Assert.That(result[1].Fields[1].DataType).IsEqualTo(Loader.Core.Models.DataType.Text);
        await ScriptIntegrationAssert.AssertFinalTableAsync(
            clickHouse,
            result[1],
            ["id", "time_value"],
            [
                new object?[] { 1, "03:04:05" }
            ]);
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(clickHouse, execution);
    }

    private static async Task ExecuteSqlServerAsync(SqlServerTestDatabase sqlServer, string sql)
    {
        await using var connection = new SqlConnection(sqlServer.ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}

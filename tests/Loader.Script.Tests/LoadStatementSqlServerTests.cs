using Loader.Script.Tests.Infrastructure;
using Microsoft.Data.SqlClient;

namespace Loader.Script.Tests;

[Explicit]
[ClassDataSource<ClickHouseTestDatabase>(Shared = SharedType.PerTestSession)]
[ParallelLimiter<SqlServerParallelLimit>]
public sealed class LoadStatementSqlServerTests
{
    private readonly ClickHouseTestDatabase clickHouse;

    public LoadStatementSqlServerTests(ClickHouseTestDatabase clickHouse)
    {
        this.clickHouse = clickHouse;
    }

    [Test]
    [DisplayName("LOAD из SqlServer source перегружает данные через temp в final table")]
    public async Task SqlServer_load_materializes_expected_final_table()
    {
        // Arrange
        await using var sqlServer = await SqlServerTestDatabase.StartAsync();
        var sourceTable = $"script_sql_source_{Guid.NewGuid():N}";
        await ExecuteSqlServerAsync(
            sqlServer,
            $$"""
            CREATE TABLE dbo.{{sourceTable}}
            (
                id int not null,
                name nvarchar(100) not null,
                city nvarchar(100) not null
            );
            INSERT INTO dbo.{{sourceTable}} (id, name, city) VALUES
            (1, N'Alice', N'Moscow'),
            (2, N'Bob', N'Berlin'),
            (3, N'Charlie', N'London');
            """);

        // Act
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            clickHouse,
            $$"""
            sql_people:
            LOAD
                Text(id) AS id,
                Upper(name) AS name,
                city
            FROM [{{sqlServer.ConnectionString}}] (sqlserver)
            SQL SELECT * FROM dbo.{{sourceTable}} WHERE city != 'Berlin' ORDER BY id ASC;
            """);

        // Assert
        var result = execution.Tables;
        await Assert.That(result).Count().IsEqualTo(1);
        await Assert.That(result[0].Alias).IsEqualTo("sql_people");
        await ScriptIntegrationAssert.AssertFinalTableAsync(
            clickHouse,
            result[0],
            ["id", "name", "city"],
            [
                ["1", "ALICE", "Moscow"],
                ["3", "CHARLIE", "London"]
            ],
            "ORDER BY `column1` ASC");
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

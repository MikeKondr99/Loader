using Loader.Script.Tests.Infrastructure;
using Oracle.ManagedDataAccess.Client;

namespace Loader.Script.Tests;

[TestWithDependency(DatabaseDependency.ClickHouseDwh, DatabaseDependency.Oracle)]
public sealed class LoadStatementOracleTests
{
    private readonly ClickHouseTestDatabase clickHouse;

    public LoadStatementOracleTests(ClickHouseTestDatabase clickHouse)
    {
        this.clickHouse = clickHouse;
    }

    [Test]
    [DisplayName("LOAD из Oracle source перегружает данные через temp в final table")]
    public async Task Oracle_load_materializes_expected_final_table()
    {
        // Arrange
        await using var oracle = await OracleTestDatabase.StartAsync();
        var sourceTable = $"SCRIPT_ORA_SOURCE_{Guid.NewGuid():N}".ToUpperInvariant();
        await ExecuteOracleAsync(
            oracle,
            $$"""
            CREATE TABLE {{sourceTable}}
            (
                ID number(10, 0) not null,
                NAME varchar2(100) not null,
                CITY varchar2(100) not null
            )
            """);
        await ExecuteOracleAsync(
            oracle,
            $$"""
            INSERT ALL
                INTO {{sourceTable}} (ID, NAME, CITY) VALUES (1, 'Alice', 'Moscow')
                INTO {{sourceTable}} (ID, NAME, CITY) VALUES (2, 'Bob', 'Berlin')
                INTO {{sourceTable}} (ID, NAME, CITY) VALUES (3, 'Charlie', 'London')
            SELECT 1 FROM dual
            """);

        // Act
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            clickHouse,
            $$"""
            oracle_people:
            LOAD
                Text(ID) AS id,
                Upper(NAME) AS name,
                CITY AS city
            FROM Oracle(connection='{{oracle.ConnectionString}}')
            SQL SELECT * FROM {{sourceTable}} WHERE CITY != 'Berlin' ORDER BY ID ASC;
            """);

        // Assert
        var result = execution.Tables;
        await Assert.That(result).Count().IsEqualTo(1);
        await Assert.That(result[0].Alias).IsEqualTo("oracle_people");
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

    private static async Task ExecuteOracleAsync(OracleTestDatabase oracle, string sql)
    {
        await using var connection = new OracleConnection(oracle.ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}

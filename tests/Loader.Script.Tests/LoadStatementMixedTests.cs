using Loader.Script.Tests.Infrastructure;

namespace Loader.Script.Tests;

[TestWithDependency(DatabaseDependency.ClickHouseDwh)]
public sealed class LoadStatementMixedTests
{
    private readonly ClickHouseTestDatabase database;

    public LoadStatementMixedTests(ClickHouseTestDatabase database)
    {
        this.database = database;
    }

    [Test]
    [TestWithDependency(DatabaseDependency.ClickHouse, UseDataSource = false)]
    [DisplayName("Script выполняет несколько LOAD из разных источников и возвращает final tables по порядку")]
    public async Task Execute_script_loads_multiple_sources_into_clickhouse()
    {
        // Arrange
        var sourceTable = $"script_mixed_source_{Guid.NewGuid():N}";
        await ScriptIntegrationAssert.ExecuteClickHouseAsync(
            database,
            $$"""
            CREATE TABLE `{{sourceTable}}`
            (
                `username` String,
                `city` String
            )
            ENGINE = Memory
            """);
        await ScriptIntegrationAssert.ExecuteClickHouseAsync(
            database,
            $$"""
            INSERT INTO `{{sourceTable}}` (`username`, `city`) VALUES
            ('mike', 'Moscow'),
            ('anna', 'London'),
            ('bob', 'Berlin')
            """);

        // Act
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            $$"""
            csv_names:
            LOAD
                name,
                Upper(name) AS upper_name
            FROM Csv(path='orders.csv')
            WHERE name != 'Bob'
            ORDER BY name DESC;

            json_names:
            LOAD
                [user.name] AS name,
                city
            FROM Json(path='inventory.json')
            WHERE city = 'Moscow'
            ORDER BY [user.name] ASC;

            ch_users:
            LOAD
                username,
                city
            FROM ClickHouse(connection='{{database.ConnectionString}}')
            SQL SELECT * FROM `{{sourceTable}}` WHERE city != 'Berlin' ORDER BY username ASC;
            """);

        // Assert
        var result = execution.Tables;
        await Assert.That(result).Count().IsEqualTo(3);
        await Assert.That(result.Select(static table => table.Alias!).ToArray())
            .IsEquivalentTo(["csv_names", "json_names", "ch_users"], TUnit.Assertions.Enums.CollectionOrdering.Matching);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            result[0],
            ["name", "upper_name"],
            [
                ["Charlie", "CHARLIE"],
                ["Alice", "ALICE"]
            ],
            "ORDER BY `column1` DESC");

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            result[1],
            ["name", "city"],
            [
                ["Alice", "Moscow"],
                ["Charlie", "Moscow"]
            ],
            "ORDER BY `column1` ASC");

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            result[2],
            ["username", "city"],
            [
                ["anna", "London"],
                ["mike", "Moscow"]
            ],
            "ORDER BY `column1` ASC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }

    [Test]
    [DisplayName("Script выполняет LOAD из результата предыдущего LOAD через Table provider")]
    public async Task Execute_script_loads_from_previous_load_table()
    {
        // Arrange
        // Act
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            raw_names:
            LOAD
                id,
                name,
                Upper(name) AS [upper-name]
            FROM Csv(path='orders.csv')
            ORDER BY id ASC;

            final_names:
            LOAD
                Text(Int(id)) AS id,
                [upper-name] AS name
            FROM raw_names
            WHERE name != 'Bob'
            ORDER BY id DESC;
            """);

        // Assert
        var result = execution.Tables;
        await Assert.That(result).Count().IsEqualTo(2);
        await Assert.That(result.Select(static table => table.Alias!).ToArray())
            .IsEquivalentTo(["raw_names", "final_names"], TUnit.Assertions.Enums.CollectionOrdering.Matching);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            result[1],
            ["id", "name"],
            [
                ["3", "CHARLIE"],
                ["1", "ALICE"]
            ],
            "ORDER BY `column1` DESC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }

    [Test]
    [DisplayName("Script выполняет LOAD из результата предыдущего LOAD с blocked table name")]
    public async Task Execute_script_loads_from_previous_blocked_load_table()
    {
        // Arrange
        // Act
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            [raw names]:
            LOAD
                id,
                name
            FROM Csv(path='orders.csv')
            ORDER BY id ASC;

            final_names:
            LOAD
                id,
                name
            FROM [raw names]
            WHERE name != 'Bob'
            ORDER BY id DESC;
            """);

        // Assert
        var result = execution.Tables;
        await Assert.That(result).Count().IsEqualTo(2);
        await Assert.That(result.Select(static table => table.Alias!).ToArray())
            .IsEquivalentTo(["raw names", "final_names"], TUnit.Assertions.Enums.CollectionOrdering.Matching);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            result[1],
            ["id", "name"],
            [
                ["3", "Charlie"],
                ["1", "Alice"]
            ],
            "ORDER BY `column1` DESC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }
}

using Loader.Script.Tests.Infrastructure;

namespace Loader.Script.Tests;

[ClassDataSource<ClickHouseTestDatabase>(Shared = SharedType.PerTestSession)]
[ParallelLimiter<ClickHouseParallelLimit>]
public sealed class LoadStatementMixedTests
{
    private readonly ClickHouseTestDatabase database;

    public LoadStatementMixedTests(ClickHouseTestDatabase database)
    {
        this.database = database;
    }

    [Test]
    [DisplayName("Script РІС‹РїРѕР»РЅСЏРµС‚ РЅРµСЃРєРѕР»СЊРєРѕ LOAD РёР· СЂР°Р·РЅС‹С… РёСЃС‚РѕС‡РЅРёРєРѕРІ Рё РІРѕР·РІСЂР°С‰Р°РµС‚ final tables РїРѕ РїРѕСЂСЏРґРєСѓ")]
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
}

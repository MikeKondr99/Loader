using Loader.Script.Tests.Infrastructure;

namespace Loader.Script.Tests;

[ClassDataSource<ClickHouseTestDatabase>(Shared = SharedType.PerTestSession)]
[ParallelLimiter<ClickHouseParallelLimit>]
public sealed class LoadStatementClickHouseTests
{
    private readonly ClickHouseTestDatabase database;

    public LoadStatementClickHouseTests(ClickHouseTestDatabase database)
    {
        this.database = database;
    }

    [Test]
    [DisplayName("LOAD РёР· ClickHouse source РїРµСЂРµРіСЂСѓР¶Р°РµС‚ РґР°РЅРЅС‹Рµ С‡РµСЂРµР· temp РІ final table")]
    public async Task ClickHouse_load_materializes_expected_final_table()
    {
        // Arrange
        var sourceTable = $"script_ch_source_{Guid.NewGuid():N}";
        await ScriptIntegrationAssert.ExecuteClickHouseAsync(
            database,
            $$"""
            CREATE TABLE `{{sourceTable}}`
            (
                `id` Int32,
                `name` String,
                `city` String
            )
            ENGINE = Memory
            """);
        await ScriptIntegrationAssert.ExecuteClickHouseAsync(
            database,
            $$"""
            INSERT INTO `{{sourceTable}}` (`id`, `name`, `city`) VALUES
            (1, 'Alice', 'Moscow'),
            (2, 'Bob', 'Berlin'),
            (3, 'Charlie', 'London')
            """);

        // Act
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            $$"""
            ch_people:
            LOAD
                Text(id) AS id,
                Upper(name) AS name,
                city
            FROM ClickHouse(connection='{{database.ConnectionString}}')
            SQL SELECT * FROM `{{sourceTable}}` WHERE city != 'Berlin' ORDER BY id ASC;
            """);

        // Assert
        var result = execution.Tables;
        await Assert.That(result).Count().IsEqualTo(1);
        await Assert.That(result[0].Alias).IsEqualTo("ch_people");
        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            result[0],
            ["id", "name", "city"],
            [
                ["1", "ALICE", "Moscow"],
                ["3", "CHARLIE", "London"]
            ],
            "ORDER BY `column1` ASC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }
}

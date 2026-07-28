using Loader.Script.Tests.Infrastructure;

namespace Loader.Script.Tests;

[ClassDataSource<ClickHouseTestDatabase>(Shared = SharedType.PerTestSession)]
[ParallelLimiter<ClickHouseParallelLimit>]
public sealed class LoadStatementJsonTests
{
    private readonly ClickHouseTestDatabase database;

    public LoadStatementJsonTests(ClickHouseTestDatabase database)
    {
        this.database = database;
    }

    [Test]
    [DisplayName("LOAD из JSON анализирует схему, пишет temp и сохраняет final table")]
    public async Task Json_load_materializes_expected_final_table()
    {
        // Arrange
        // Act
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            json_inventory:
            LOAD
                Text(Int(id)) AS id,
                [user.name] AS name,
                city
            FROM [inventory.json] (json)
            WHERE city = 'Moscow'
            ORDER BY id ASC;
            """);

        // Assert
        var result = execution.Tables;
        await Assert.That(result).Count().IsEqualTo(1);
        await Assert.That(result[0].Alias).IsEqualTo("json_inventory");
        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            result[0],
            ["id", "name", "city"],
            [
                ["1", "Alice", "Moscow"],
                ["3", "Charlie", "Moscow"]
            ],
            "ORDER BY `id` ASC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }
}

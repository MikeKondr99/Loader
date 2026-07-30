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
            "ORDER BY `column1` ASC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }

    [Test]
    [DisplayName("LOAD из JSON с root читает массив внутри объекта")]
    public async Task Json_load_with_root_materializes_nested_array()
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
            FROM [nested-inventory.json] (json, root='payload.items')
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
            "ORDER BY `column1` ASC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }

    [Test]
    [DisplayName("LOAD из JSON с root работает без явного json marker")]
    public async Task Json_load_with_root_uses_file_extension_provider()
    {
        // Arrange
        // Act
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            json_inventory:
            LOAD
                city,
                amount
            FROM [nested-inventory.json] (root='payload.items')
            WHERE city = 'Berlin';
            """);

        // Assert
        var result = execution.Tables;
        await Assert.That(result).Count().IsEqualTo(1);
        await Assert.That(result[0].Alias).IsEqualTo("json_inventory");
        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            result[0],
            ["city", "amount"],
            [
                ["Berlin", "20.00"]
            ]);
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }

    [Test]
    [DisplayName("LOAD из JSON root поддерживает индекс массива в пути")]
    public async Task Json_load_with_root_array_index_materializes_selected_table()
    {
        // Arrange
        // Act
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            json_orders:
            LOAD
                Text(Int(id)) AS id,
                city
            FROM [indexed-tables.json] (json, root='tables.0.data')
            ORDER BY id ASC;
            """);

        // Assert
        var result = execution.Tables;
        await Assert.That(result).Count().IsEqualTo(1);
        await Assert.That(result[0].Alias).IsEqualTo("json_orders");
        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            result[0],
            ["id", "city"],
            [
                ["1", "Moscow"],
                ["2", "London"]
            ],
            "ORDER BY `column1` ASC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }
}

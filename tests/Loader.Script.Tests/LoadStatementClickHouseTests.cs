using Loader.Script.Tests.Infrastructure;

namespace Loader.Script.Tests;

[TestWithDependency(DatabaseDependency.ClickHouseDwh, DatabaseDependency.ClickHouse)]
public sealed class LoadStatementClickHouseTests
{
    private readonly ClickHouseTestDatabase database;

    public LoadStatementClickHouseTests(ClickHouseTestDatabase database)
    {
        this.database = database;
    }

    [Test]
    [DisplayName("LOAD из Connect ClickHouse source перегружает данные через temp в final table")]
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
                `city` String,
                `active` Bool,
                `amount` Nullable(Decimal(10, 2)),
                `created_at` DateTime,
                `note` Nullable(String)
            )
            ENGINE = Memory
            """);
        await ScriptIntegrationAssert.ExecuteClickHouseAsync(
            database,
            $$"""
            INSERT INTO `{{sourceTable}}` (`id`, `name`, `city`, `active`, `amount`, `created_at`, `note`) VALUES
            (1, 'Alice', 'Moscow', true, 10.50, toDateTime('2024-01-01 10:11:12'), 'vip'),
            (2, 'Bob', 'Berlin', false, NULL, toDateTime('2024-01-02 11:12:13'), NULL),
            (3, 'Charlie', 'London', true, 25.75, toDateTime('2024-01-03 12:13:14'), 'new')
            """);

        // Act
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            $$"""
            ch_people:
            LOAD
                Text(id) AS id,
                Upper(name) AS name,
                city AS Город,
                active,
                amount,
                created_at,
                note
            FROM Connect(name='container_ch')
            SQL SELECT * FROM `{{sourceTable}}` WHERE city != 'Berlin' ORDER BY id ASC;

            ch_people_copy:
            LOAD *
            FROM ch_people
            ORDER BY id ASC;
            """);

        // Assert
        var result = execution.Tables;
        await Assert.That(result).Count().IsEqualTo(2);
        await Assert.That(result[0].Alias).IsEqualTo("ch_people");
        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            result[1],
            ["id", "name", "Город", "active", "amount", "created_at", "note"],
            [
                ["1", "ALICE", "Moscow", true, 10.50m, new DateTime(2024, 1, 1, 10, 11, 12), "vip"],
                ["3", "CHARLIE", "London", true, 25.75m, new DateTime(2024, 1, 3, 12, 13, 14), "new"]
            ],
            "ORDER BY `column1` ASC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }

    [Test]
    [DisplayName("LOAD из Connect поддерживает positional connection name")]
    public async Task ClickHouse_load_accepts_positional_connect_name()
    {
        var sourceTable = $"script_ch_source_{Guid.NewGuid():N}";
        await ScriptIntegrationAssert.ExecuteClickHouseAsync(
            database,
            $$"""
            CREATE TABLE `{{sourceTable}}`
            (
                `id` Int32,
                `name` String
            )
            ENGINE = Memory
            """);
        await ScriptIntegrationAssert.ExecuteClickHouseAsync(
            database,
            $$"""
            INSERT INTO `{{sourceTable}}` (`id`, `name`) VALUES
            (1, 'Alice'),
            (2, 'Bob')
            """);

        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            $$"""
            ch_short:
            LOAD *
            FROM Connect('container_ch')
            SQL SELECT * FROM `{{sourceTable}}` ORDER BY id ASC;
            """);

        var result = execution.Tables;
        await Assert.That(result).Count().IsEqualTo(1);
        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            result[0],
            ["id", "name"],
            [
                [1, "Alice"],
                [2, "Bob"]
            ],
            "ORDER BY `column1` ASC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }
}

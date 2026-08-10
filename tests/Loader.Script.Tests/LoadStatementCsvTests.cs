using Loader.Script.Tests.Infrastructure;

namespace Loader.Script.Tests;

[TestWithDependency(DatabaseDependency.ClickHouseDwh)]
public sealed class LoadStatementCsvTests
{
    private readonly ClickHouseTestDatabase database;

    public LoadStatementCsvTests(ClickHouseTestDatabase database)
    {
        this.database = database;
    }

    [Test]
    [DisplayName("LOAD из CSV пишет temp, применяет преобразования и сохраняет final table")]
    public async Task Csv_load_materializes_expected_final_table()
    {
        // Arrange
        // Act
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            csv_orders:
            LOAD
                Text(Int(id)) AS id,
                Upper(name) AS name,
                city
            FROM Csv(path='pipe-orders.csv', delimiter='|')
            WHERE Num(amount) > 15
            ORDER BY id DESC;
            """);

        // Assert
        var result = execution.Tables;
        await Assert.That(result).Count().IsEqualTo(1);
        await Assert.That(result[0].Alias).IsEqualTo("csv_orders");
        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            result[0],
            ["id", "name", "city"],
            [
                ["3", "CHARLIE", "London"],
                ["2", "BOB", "Berlin"]
            ],
            "ORDER BY `column1` DESC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }
}

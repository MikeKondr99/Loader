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

    [Test]
    [DisplayName("LOAD из CSV создает Time преобразованием и сохраняет тип при следующем LOAD")]
    public async Task Csv_load_time_transformation_preserves_time_type_in_next_load()
    {
        // Arrange
        // Act
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            csv_time:
            LOAD
                Text(Int(id)) AS id,
                Time(start_time) AS start_time
            FROM Csv(path='time-orders.csv')
            ORDER BY id ASC;

            s:
            LOAD * FROM csv_time;
            """);

        // Assert
        var result = execution.Tables;
        await Assert.That(result).Count().IsEqualTo(2);
        await Assert.That(result[0].Fields[1].DataType).IsEqualTo(Loader.Core.Models.DataType.Time);
        await Assert.That(result[1].Fields[1].DataType).IsEqualTo(Loader.Core.Models.DataType.Time);
        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            result[1],
            ["id", "start_time"],
            [
                new object?[] { "1", new DateTime(1970, 1, 1, 3, 4, 5) },
                new object?[] { "2", new DateTime(1970, 1, 1, 15, 16, 17) }
            ],
            "ORDER BY `column1` ASC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }
}

using Loader.Script.Tests.Infrastructure;

namespace Loader.Script.Tests;

[ClassDataSource<ClickHouseTestDatabase>(Shared = SharedType.PerTestSession)]
[ParallelLimiter<ClickHouseParallelLimit>]
public sealed class LoadStatementXmlTests
{
    private readonly ClickHouseTestDatabase database;

    public LoadStatementXmlTests(ClickHouseTestDatabase database)
    {
        this.database = database;
    }

    [Test]
    [DisplayName("LOAD из XML анализирует flat table, пишет temp и сохраняет final table")]
    public async Task Xml_load_materializes_expected_final_table()
    {
        // Arrange
        // Act
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            xml_people:
            LOAD
                Text(Int(id)) AS id,
                name,
                source
            FROM [people.xml] (xml, table='person')
            WHERE city != 'Berlin'
            ORDER BY id ASC;
            """);

        // Assert
        var result = execution.Tables;
        await Assert.That(result).Count().IsEqualTo(1);
        await Assert.That(result[0].Alias).IsEqualTo("xml_people");
        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            result[0],
            ["id", "name", "source"],
            [
                ["1", "Alice", "crm"],
                ["3", "Charlie", "crm"]
            ],
            "ORDER BY `id` ASC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }
}

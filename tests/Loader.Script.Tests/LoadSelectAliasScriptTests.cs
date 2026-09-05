using Loader.Script.Tests.Infrastructure;
using TUnit.Assertions.Enums;

namespace Loader.Script.Tests;

[TestWithDependency(DatabaseDependency.ClickHouseDwh)]
public sealed class LoadSelectAliasScriptTests
{
    private readonly ClickHouseTestDatabase database;

    public LoadSelectAliasScriptTests(ClickHouseTestDatabase database)
    {
        this.database = database;
    }

    [Test]
    [DisplayName("Script LOAD использует SELECT alias в следующем LOAD выражении")]
    public async Task Load_select_alias_is_available_to_next_load_expression()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            result:
            LOAD
                x + 1 AS y,
                y + 1 AS z
            FROM Inline(x; 1);
            """);

        await Assert.That(execution.Tables).Count().IsEqualTo(1);
        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[0],
            ["y", "z"],
            [
                [2L, 3L]
            ]);
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }

    [Test]
    [DisplayName("Script LOAD использует агрегатный SELECT alias в ORDER BY")]
    public async Task Load_order_by_can_use_aggregate_select_alias()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            source:
            LOAD *
            FROM Inline(segment, value;
                'a', 1;
                'a', 3;
                'b', 10);

            result:
            LOAD
                segment,
                AVG(value) AS avg_value
            FROM source
            GROUP BY segment
            ORDER BY avg_value;
            """);

        await Assert.That(execution.Tables).Count().IsEqualTo(2);
        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[1],
            ["segment", "avg_value"],
            [
                ["a", 2.0],
                ["b", 10.0]
            ],
            "ORDER BY `column2` ASC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }
}

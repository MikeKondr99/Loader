using System.Globalization;
using Loader.Script.Tests.Infrastructure;
using TUnit.Assertions.Enums;

namespace Loader.Script.Tests;

[TestWithDependency(DatabaseDependency.ClickHouseDwh)]
public sealed class LoadAggregationFunctionScriptTests
{
    private readonly ClickHouseTestDatabase database;

    public LoadAggregationFunctionScriptTests(ClickHouseTestDatabase database)
    {
        this.database = database;
    }

    [Test]
    [DisplayName("Script интегрирует STDDEV CORREL и COUNT_IF в полном LOAD pipeline")]
    public async Task Execute_script_aggregates_stddev_correl_and_count_if()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            observations:
            LOAD
                segment,
                x,
                y,
                x_text,
                y_text,
                flag
            FROM Inline(segment, x, y, x_text, y_text, flag;
                'constant', 5, 1, '5.00', '1.00', false;
                'constant', 5, 2, '5.00', '2.00', null;
                'constant', 5, 3, '5.00', '3.00', true;
                'perfect', 2, 4, '2.00', '4.00', true;
                'perfect', 4, 8, '4.00', '8.00', false;
                'perfect', 4, 8, '4.00', '8.00', true;
                'perfect', 4, 8, '4.00', '8.00', null;
                'perfect', 5, 10, '5.00', '10.00', false;
                'perfect', 5, 10, '5.00', '10.00', true;
                'perfect', 7, 14, '7.00', '14.00', false;
                'perfect', 9, 18, '9.00', '18.00', true;
                'single', 10, 20, '10.00', '20.00', true);

            aggregated:
            LOAD
                segment,
                STDDEV(x) AS stddev_int,
                STDDEV(Num(x_text)) AS stddev_decimal,
                CORREL(x, y) AS correl_int,
                CORREL(Num(x_text), Num(y_text)) AS correl_decimal,
                COUNT_IF(flag) AS true_flags,
                COUNT_IF(x > 4) AS high_x
            FROM observations
            GROUP BY segment
            ORDER BY segment ASC;
            """);

        await Assert.That(execution.Tables).Count().IsEqualTo(2);
        var table = execution.Tables[1];
        await Assert.That(table.Fields.Select(static field => field.Name).ToArray())
            .IsEquivalentTo(
                ["segment", "stddev_int", "stddev_decimal", "correl_int", "correl_decimal", "true_flags", "high_x"],
                CollectionOrdering.Matching);

        var rows = await ScriptIntegrationAssert.ReadFinalTableAsync(database, table, "ORDER BY `column1` ASC");
        await Assert.That(rows.Rows).Count().IsEqualTo(3);

        var constant = FindRow(rows, "constant");
        await AssertNumberAsync(constant[1], 0);
        await AssertNumberAsync(constant[2], 0);
        await Assert.That(constant[3]).IsNull();
        await Assert.That(constant[4]).IsNull();
        await AssertIntegerAsync(constant[5], 1);
        await AssertIntegerAsync(constant[6], 3);

        var perfect = FindRow(rows, "perfect");
        await AssertNumberAsync(perfect[1], 2);
        await AssertNumberAsync(perfect[2], 2);
        await AssertNumberAsync(perfect[3], 1);
        await AssertNumberAsync(perfect[4], 1);
        await AssertIntegerAsync(perfect[5], 4);
        await AssertIntegerAsync(perfect[6], 4);

        var single = FindRow(rows, "single");
        await AssertNumberAsync(single[1], 0);
        await AssertNumberAsync(single[2], 0);
        await Assert.That(single[3]).IsNull();
        await Assert.That(single[4]).IsNull();
        await AssertIntegerAsync(single[5], 1);
        await AssertIntegerAsync(single[6], 1);
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }

    private static object?[] FindRow(
        ScriptIntegrationAssert.QueryRows rows,
        string segment)
    {
        return rows.Rows.Single(row => string.Equals((string?)row[0], segment, StringComparison.Ordinal));
    }

    private static async Task AssertNumberAsync(object? actual, double expected)
    {
        await Assert.That(Convert.ToDouble(actual, CultureInfo.InvariantCulture))
            .IsEqualTo(expected)
            .Within(0.000001);
    }

    private static async Task AssertIntegerAsync(object? actual, long expected)
    {
        await Assert.That(Convert.ToInt64(actual, CultureInfo.InvariantCulture)).IsEqualTo(expected);
    }
}

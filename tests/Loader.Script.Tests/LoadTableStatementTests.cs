using Loader.Core.Models;
using Loader.Core.Writers.ClickHouse;
using Loader.Script.Execution;
using Loader.Script.Tests.Infrastructure;

namespace Loader.Script.Tests;

[TestWithDependency(DatabaseDependency.ClickHouseDwh)]
public sealed class LoadTableStatementTests
{
    private readonly ClickHouseTestDatabase database;

    public LoadTableStatementTests(ClickHouseTestDatabase database)
    {
        this.database = database;
    }

    [Test]
    [DisplayName("Script Table выполняет LOAD из результата предыдущего LOAD")]
    public async Task Execute_script_loads_from_previous_load_table()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            raw_names:
            LOAD
                id,
                name,
                Upper(name) AS [upper-name]
            FROM Csv(path='orders.csv')
            ORDER BY id ASC;

            final_names:
            LOAD
                Text(Int(id)) AS text_id,
                [upper-name] AS upper_name
            FROM raw_names
            WHERE name != 'Bob'
            ORDER BY id DESC;
            """);

        var result = execution.Tables;
        await Assert.That(result).Count().IsEqualTo(2);
        await Assert.That(result.Select(static table => table.Alias!).ToArray())
            .IsEquivalentTo(["raw_names", "final_names"], TUnit.Assertions.Enums.CollectionOrdering.Matching);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            result[1],
            ["text_id", "upper_name"],
            [
                ["3", "CHARLIE"],
                ["1", "ALICE"]
            ],
            "ORDER BY `column1` DESC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }

    [Test]
    [DisplayName("Script Table FIRST ограничивает исходные строки до LOAD преобразований")]
    public async Task Execute_script_table_source_first_limits_source_rows_before_transformations()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            source:
            LOAD
                id,
                name
            FROM Inline(id, name;
                1, 'Zulu';
                2, 'Yankee';
                3, 'Alpha';
                4, 'Beta');

            result:
            FIRST 2
            LOAD
                id,
                name
            FROM source
            ORDER BY name ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[1],
            ["id", "name"],
            [
                new object?[] { 2L, "Yankee" },
                new object?[] { 1L, "Zulu" }
            ],
            "ORDER BY `column2` ASC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }

    [Test]
    [DisplayName("Script Table сопоставляет поля по alias, а не по physical column")]
    public async Task Execute_script_table_source_preserves_alias_mapping_when_physical_order_differs()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            source:
            LOAD
                raw_b AS second,
                raw_a AS first
            FROM Inline(raw_a, raw_b;
                1, 10;
                2, 20);

            result:
            LOAD
                first,
                second,
                first + second AS total
            FROM source
            ORDER BY first ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[1],
            ["first", "second", "total"],
            [
                new object?[] { 1L, 10L, 11L },
                new object?[] { 2L, 20L, 22L }
            ],
            "ORDER BY `column1` ASC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }

    [Test]
    [DisplayName("Script Table очищает TEMP LOAD в конце выполнения после использования")]
    public async Task Execute_script_cleans_temp_load_after_it_was_used_by_table_source()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            raw_names:
            TEMP LOAD
                id,
                name
            FROM Csv(path='orders.csv')
            ORDER BY id ASC;

            final_names:
            LOAD
                Text(Int(id)) AS text_id,
                Upper(name) AS upper_name
            FROM raw_names
            WHERE name != 'Bob'
            ORDER BY id DESC;
            """);

        await Assert.That(execution.Tables).Count().IsEqualTo(1);
        await Assert.That(execution.Tables[0].Alias).IsEqualTo("final_names");
        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[0],
            ["text_id", "upper_name"],
            [
                ["3", "CHARLIE"],
                ["1", "ALICE"]
            ],
            "ORDER BY `column1` DESC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
        await ScriptIntegrationAssert.AssertTableCountWithPrefixAsync(database, execution.FinalTablePrefix, 1);
    }

    [Test]
    [DisplayName("Script Table выполняет LOAD из результата предыдущего LOAD с blocked table name")]
    public async Task Execute_script_loads_from_previous_blocked_load_table()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            [raw names]:
            LOAD
                id,
                name
            FROM Csv(path='orders.csv')
            ORDER BY id ASC;

            final_names:
            LOAD
                id,
                name
            FROM [raw names]
            WHERE name != 'Bob'
            ORDER BY id DESC;
            """);

        var result = execution.Tables;
        await Assert.That(result).Count().IsEqualTo(2);
        await Assert.That(result.Select(static table => table.Alias!).ToArray())
            .IsEquivalentTo(["raw names", "final_names"], TUnit.Assertions.Enums.CollectionOrdering.Matching);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            result[1],
            ["id", "name"],
            [
                ["3", "Charlie"],
                ["1", "Alice"]
            ],
            "ORDER BY `column1` DESC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }

    [Test]
    [DisplayName("Script Table сохраняет логический Time тип при LOAD из предыдущей таблицы")]
    public async Task Execute_script_preserves_time_type_from_previous_load_table()
    {
        var sourceTable = new ClickHouseTableName
        {
            Table = $"script_time_source_{Guid.NewGuid():N}"
        };
        var tempPrefix = $"script_test_temp_{Guid.NewGuid():N}_";
        var finalPrefix = $"script_test_final_{Guid.NewGuid():N}_";
        await ScriptIntegrationAssert.ExecuteClickHouseAsync(
            database,
            $$"""
            CREATE TABLE {{sourceTable.ToSql()}}
            (
                `column1` Int32,
                `column2` DateTime,
                `column3` DateTime
            )
            ENGINE = Log
            """);
        await ScriptIntegrationAssert.ExecuteClickHouseAsync(
            database,
            $$"""
            INSERT INTO {{sourceTable.ToSql()}} (`column1`, `column2`, `column3`) VALUES
            (1, toDateTime('1970-01-01 03:04:05'), toDateTime('1970-01-01 04:05:06'))
            """);

        var context = ScriptIntegrationAssert.CreateContext(database) with
        {
            Options = new ScriptContextOptions
            {
                TempTablePrefix = tempPrefix,
                FinalTablePrefix = finalPrefix
            }
        };
        context.AddLoadedTable(new LoadedTable
        {
            Name = sourceTable,
            Alias = "pg_time",
            RowCount = 1,
            Fields =
            [
                Field("id", DataType.Integer),
                Field("time_value", DataType.Time),
                Field("interval_value", DataType.Time)
            ]
        });
        var executor = new ScriptExecutor();
        var script = Loader.Lang.Script.Parse(
            """
            s:
            LOAD * FROM pg_time;
            """).Value!;
        LoadedTable? finalTable = null;

        try
        {
            var result = await executor.ExecuteAsync(context, script);

            await Assert.That(result).Count().IsEqualTo(2);
            var table = result[1];
            finalTable = table;
            await Assert.That(table.Alias).IsEqualTo("s");
            await Assert.That(table.Fields.Select(static field => field.Name).ToArray())
                .IsEquivalentTo(["id", "time_value", "interval_value"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(table.Fields[1].DataType).IsEqualTo(DataType.Time);
            await Assert.That(table.Fields[2].DataType).IsEqualTo(DataType.Time);
            await ScriptIntegrationAssert.AssertFinalTableAsync(
                database,
                table,
                ["id", "time_value", "interval_value"],
                [
                    new object?[] { 1, new DateTime(1970, 1, 1, 3, 4, 5), new DateTime(1970, 1, 1, 4, 5, 6) }
                ]);
            await ScriptIntegrationAssert.AssertNoTablesWithPrefixAsync(database, tempPrefix);
        }
        finally
        {
            await ScriptIntegrationAssert.ExecuteClickHouseAsync(database, $"DROP TABLE IF EXISTS {sourceTable.ToSql()}");
            if (finalTable is not null)
            {
                await ScriptIntegrationAssert.ExecuteClickHouseAsync(database, $"DROP TABLE IF EXISTS {finalTable.Name.ToSql()}");
            }

            await ScriptIntegrationAssert.AssertNoTablesWithPrefixAsync(database, finalPrefix);
        }
    }

    private static LoadedTableField Field(string name, DataType dataType)
    {
        return new LoadedTableField
        {
            Name = name,
            DataType = dataType,
            CanBeNull = false
        };
    }
}

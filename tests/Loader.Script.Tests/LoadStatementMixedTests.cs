using Loader.Script.Tests.Infrastructure;
using Loader.Core.Models;
using Loader.Core.Writers.ClickHouse;
using Loader.Script.Execution;

namespace Loader.Script.Tests;

[TestWithDependency(DatabaseDependency.ClickHouseDwh)]
public sealed class LoadStatementMixedTests
{
    private readonly ClickHouseTestDatabase database;

    public LoadStatementMixedTests(ClickHouseTestDatabase database)
    {
        this.database = database;
    }

    [Test]
    [TestWithDependency(DatabaseDependency.ClickHouse, UseDataSource = false)]
    [DisplayName("Script выполняет несколько LOAD из разных источников и возвращает final tables по порядку")]
    public async Task Execute_script_loads_multiple_sources_into_clickhouse()
    {
        // Arrange
        var sourceTable = $"script_mixed_source_{Guid.NewGuid():N}";
        await ScriptIntegrationAssert.ExecuteClickHouseAsync(
            database,
            $$"""
            CREATE TABLE `{{sourceTable}}`
            (
                `username` String,
                `city` String
            )
            ENGINE = Memory
            """);
        await ScriptIntegrationAssert.ExecuteClickHouseAsync(
            database,
            $$"""
            INSERT INTO `{{sourceTable}}` (`username`, `city`) VALUES
            ('mike', 'Moscow'),
            ('anna', 'London'),
            ('bob', 'Berlin')
            """);

        // Act
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            $$"""
            csv_names:
            LOAD
                name,
                Upper(name) AS upper_name
            FROM Csv(path='orders.csv')
            WHERE name != 'Bob'
            ORDER BY name DESC;

            json_names:
            LOAD
                [user.name] AS name,
                city
            FROM Json(path='inventory.json')
            WHERE city = 'Moscow'
            ORDER BY [user.name] ASC;

            ch_users:
            LOAD
                username,
                city
            FROM Connect(name='container_ch')
            SQL SELECT * FROM `{{sourceTable}}` WHERE city != 'Berlin' ORDER BY username ASC;
            """);

        // Assert
        var result = execution.Tables;
        await Assert.That(result).Count().IsEqualTo(3);
        await Assert.That(result.Select(static table => table.Alias!).ToArray())
            .IsEquivalentTo(["csv_names", "json_names", "ch_users"], TUnit.Assertions.Enums.CollectionOrdering.Matching);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            result[0],
            ["name", "upper_name"],
            [
                ["Charlie", "CHARLIE"],
                ["Alice", "ALICE"]
            ],
            "ORDER BY `column1` DESC");

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            result[1],
            ["name", "city"],
            [
                ["Alice", "Moscow"],
                ["Charlie", "Moscow"]
            ],
            "ORDER BY `column1` ASC");

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            result[2],
            ["username", "city"],
            [
                ["anna", "London"],
                ["mike", "Moscow"]
            ],
            "ORDER BY `column1` ASC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }

    [Test]
    [DisplayName("Script загружает Inline source и применяет LOAD преобразования")]
    public async Task Execute_script_loads_inline_source_into_clickhouse()
    {
        // Arrange
        // Act
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            inline_orders_raw:
            LOAD *
            FROM Inline(id, name, active, amount, created_text;
                1, 'Mike', true, -10.5, '2026-01-01';
                2, null, false, 20.0, null);

            inline_orders:
            LOAD
                id,
                name,
                active,
                amount,
                created_text.Date('yyyy-MM-dd') AS created
            FROM inline_orders_raw
            ORDER BY id ASC;
            """);

        // Assert
        var result = execution.Tables;
        await Assert.That(result).Count().IsEqualTo(2);
        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            result[1],
            ["id", "name", "active", "amount", "created"],
            [
                [1L, "Mike", true, -10.5, new DateTime(2026, 1, 1)],
                [2L, null, false, 20.0, null]
            ],
            "ORDER BY `column1` ASC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }

    [Test]
    [DisplayName("Script выполняет LOAD из результата предыдущего LOAD через Table provider")]
    public async Task Execute_script_loads_from_previous_load_table()
    {
        // Arrange
        // Act
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
                Text(Int(id)) AS id,
                [upper-name] AS name
            FROM raw_names
            WHERE name != 'Bob'
            ORDER BY id DESC;
            """);

        // Assert
        var result = execution.Tables;
        await Assert.That(result).Count().IsEqualTo(2);
        await Assert.That(result.Select(static table => table.Alias!).ToArray())
            .IsEquivalentTo(["raw_names", "final_names"], TUnit.Assertions.Enums.CollectionOrdering.Matching);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            result[1],
            ["id", "name"],
            [
                ["3", "CHARLIE"],
                ["1", "ALICE"]
            ],
            "ORDER BY `column1` DESC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }

    [Test]
    [DisplayName("Script выполняет LOAD из результата предыдущего LOAD с blocked table name")]
    public async Task Execute_script_loads_from_previous_blocked_load_table()
    {
        // Arrange
        // Act
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

        // Assert
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
    [DisplayName("Script сохраняет логический Time тип при LOAD из результата предыдущего LOAD")]
    public async Task Execute_script_preserves_time_type_from_previous_load_table()
    {
        // Arrange
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
            // Act
            var result = await executor.ExecuteAsync(context, script);

            // Assert
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

    [Test]
    [DisplayName("Script вычисляет аппроксимацию pi через ряд Лейбница")]
    public async Task Execute_script_calculates_pi_with_leibniz_series()
    {
        // Arrange
        // Act
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            leibniz_terms:
            LOAD
                If(Mod(number, 4) != 3,
                    1.0 / Num(number),
                    -1.0 / Num(number)) AS x
            FROM Numbers(min=1, max=999, step=2);

            pi_result:
            LOAD
                SUM(x) * 4 AS pi_leibniz
            FROM leibniz_terms;
            """);

        // Assert
        var result = execution.Tables;
        await Assert.That(result).Count().IsEqualTo(2);
        await Assert.That(result.Select(static table => table.Alias!).ToArray())
            .IsEquivalentTo(["leibniz_terms", "pi_result"], TUnit.Assertions.Enums.CollectionOrdering.Matching);

        var rows = await ScriptIntegrationAssert.ReadFinalTableAsync(database, result[1]);
        await Assert.That(rows.Rows).Count().IsEqualTo(1);
        await Assert.That(Convert.ToDouble(rows.Rows[0][0], System.Globalization.CultureInfo.InvariantCulture))
            .IsEqualTo(Math.PI)
            .Within(0.01);
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }

    [Test]
    [DisplayName("Script строит календарь по явному min/max диапазону")]
    public async Task Execute_script_loads_calendar_from_explicit_range()
    {
        // Arrange
        // Act
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            calendar:
            LOAD
                Date,
                Year,
                MonthNumber,
                DayOfMonth,
                YearMonth,
                WeekPeriod
            FROM Calendar(min='2024-01-01', max='2024-01-03')
            ORDER BY Date ASC;
            """);

        // Assert
        var result = execution.Tables;
        await Assert.That(result).Count().IsEqualTo(1);
        var rows = await ScriptIntegrationAssert.ReadFinalTableAsync(database, result[0], "ORDER BY `column1` ASC");
        await Assert.That(result[0].Fields.Select(static field => field.Name).ToArray())
            .IsEquivalentTo(["Date", "Year", "MonthNumber", "DayOfMonth", "YearMonth", "WeekPeriod"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(rows.Rows).Count().IsEqualTo(3);
        await AssertCalendarRowAsync(rows.Rows[0], new DateTime(2024, 1, 1), 2024, 1, 1, "2024-01", "2024-01-01 - 2024-01-07");
        await AssertCalendarRowAsync(rows.Rows[1], new DateTime(2024, 1, 2), 2024, 1, 2, "2024-01", "2024-01-01 - 2024-01-07");
        await AssertCalendarRowAsync(rows.Rows[2], new DateTime(2024, 1, 3), 2024, 1, 3, "2024-01", "2024-01-01 - 2024-01-07");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }

    [Test]
    [DisplayName("Script строит календарь по min/max поля ранее загруженной таблицы")]
    public async Task Execute_script_loads_calendar_from_loaded_table_field()
    {
        // Arrange
        // Act
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            orders:
            LOAD
                Date(2024, 1, 1).AddDays(number) AS CreatedAt
            FROM Numbers(max=2);

            calendar:
            LOAD
                Date,
                Year,
                DayOfMonth
            FROM Calendar(table='orders', field='CreatedAt')
            ORDER BY Date ASC;
            """);

        // Assert
        var result = execution.Tables;
        await Assert.That(result).Count().IsEqualTo(2);
        var rows = await ScriptIntegrationAssert.ReadFinalTableAsync(database, result[1], "ORDER BY `column1` ASC");
        await Assert.That(result[1].Fields.Select(static field => field.Name).ToArray())
            .IsEquivalentTo(["Date", "Year", "DayOfMonth"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(rows.Rows).Count().IsEqualTo(3);
        await AssertCalendarRowAsync(rows.Rows[0], new DateTime(2024, 1, 1), 2024, dayOfMonth: 1);
        await AssertCalendarRowAsync(rows.Rows[1], new DateTime(2024, 1, 2), 2024, dayOfMonth: 2);
        await AssertCalendarRowAsync(rows.Rows[2], new DateTime(2024, 1, 3), 2024, dayOfMonth: 3);
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }

    [Test]
    [DisplayName("Calendar table/field отклоняет даты вне безопасного диапазона")]
    public async Task Resolve_calendar_loaded_table_field_rejects_out_of_range_datetime64()
    {
        // Arrange
        var sourceTable = $"script_calendar_source_{Guid.NewGuid():N}";
        await ScriptIntegrationAssert.ExecuteClickHouseAsync(
            database,
            $$"""
            CREATE TABLE `{{sourceTable}}`
            (
                `column1` DateTime64(3)
            )
            ENGINE = Memory
            """);
        await ScriptIntegrationAssert.ExecuteClickHouseAsync(
            database,
            $$"""
            INSERT INTO `{{sourceTable}}` (`column1`) VALUES (toDateTime64('1900-01-01 00:00:00', 3))
            """);

        var context = ScriptIntegrationAssert.CreateContext(database);
        context.AddLoadedTable(new LoadedTable
        {
            Name = new ClickHouseTableName
            {
                Table = sourceTable
            },
            Alias = "orders",
            RowCount = 1,
            Fields =
            [
                new LoadedTableField
                {
                    Name = "CreatedAt",
                    DataType = DataType.DateTime,
                    CanBeNull = false
                }
            ]
        });

        var script = Loader.Lang.Script.Parse(
            """
            calendar:
            LOAD
                *
            FROM Calendar(table='orders', field='CreatedAt');
            """).Value!;
        var statement = (Loader.Lang.Statements.LoadStatement)script.Statements[0];
        var source = await new LoadProviderResolver().ResolveAsync(statement, context);

        // Act
        var exception = await Assert.That(async () => await source.OpenReaderAsync(CancellationToken.None))
            .Throws<Exception>();

        // Assert
        await Assert.That(exception!.Message).Contains("Calendar range must be within 1970-01-05..2148-12-31");
    }

    [Test]
    [DisplayName("Script DROP удаляет загруженную final table и убирает ее из результата")]
    public async Task Execute_script_drop_removes_loaded_table_and_physical_final_table()
    {
        // Arrange
        // Act
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            [raw names]:
            LOAD
                id,
                name
            FROM Csv(path='orders.csv')
            ORDER BY id ASC;

            DROP [raw names];
            """);

        // Assert
        await Assert.That(execution.Tables).IsEmpty();
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
        await ScriptIntegrationAssert.AssertNoTablesWithPrefixAsync(database, execution.FinalTablePrefix);
    }

    private static async Task AssertCalendarRowAsync(
        IReadOnlyList<object?> row,
        DateTime date,
        long year,
        long? monthNumber = null,
        long? dayOfMonth = null,
        string? yearMonth = null,
        string? weekPeriod = null)
    {
        await Assert.That(row[0]).IsEqualTo(date);
        await Assert.That(Convert.ToInt64(row[1], System.Globalization.CultureInfo.InvariantCulture)).IsEqualTo(year);

        if (monthNumber is not null)
        {
            await Assert.That(Convert.ToInt64(row[2], System.Globalization.CultureInfo.InvariantCulture)).IsEqualTo(monthNumber.Value);
        }

        if (dayOfMonth is not null)
        {
            var dayOrdinal = monthNumber is null ? 2 : 3;
            await Assert.That(Convert.ToInt64(row[dayOrdinal], System.Globalization.CultureInfo.InvariantCulture)).IsEqualTo(dayOfMonth.Value);
        }

        if (yearMonth is not null)
        {
            await Assert.That(row[4]).IsEqualTo(yearMonth);
        }

        if (weekPeriod is not null)
        {
            await Assert.That(row[5]).IsEqualTo(weekPeriod);
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

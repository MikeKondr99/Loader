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
    [DisplayName("Script Numbers поддерживает positional min/max")]
    public async Task Execute_script_loads_numbers_with_positional_min_max()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            numbers_short:
            LOAD
                number
            FROM Numbers(1, 9, step=4)
            ORDER BY number ASC;
            """);

        var result = execution.Tables;
        await Assert.That(result).Count().IsEqualTo(1);
        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            result[0],
            ["number"],
            [
                new object?[] { 1L },
                new object?[] { 5L },
                new object?[] { 9L }
            ],
            "ORDER BY `column1` ASC");
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
    [DisplayName("Script Calendar FIRST ограничивает сгенерированные даты до LOAD преобразований")]
    public async Task Execute_script_calendar_first_limits_generated_rows_before_transformations()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            calendar:
            FIRST 2
            LOAD
                Date,
                DayOfMonth
            FROM Calendar(min='2024-01-01', max='2024-01-05')
            ORDER BY Date DESC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[0],
            ["Date", "DayOfMonth"],
            [
                new object?[] { new DateTime(2024, 1, 2), (byte)2 },
                new object?[] { new DateTime(2024, 1, 1), (byte)1 }
            ],
            "ORDER BY `column1` DESC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }

    [Test]
    [DisplayName("Script Calendar сохраняет alias mapping для явной проекции полей")]
    public async Task Execute_script_calendar_preserves_field_mapping_for_explicit_projection()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            calendar:
            LOAD
                YearMonth,
                DayOfMonth,
                Date.Text('yyyy-MM-dd') AS DateText
            FROM Calendar(min='2024-02-28', max='2024-03-01')
            WHERE DayOfMonth >= 29
            ORDER BY Date ASC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[0],
            ["YearMonth", "DayOfMonth", "DateText"],
            [
                new object?[] { "2024-02", (byte)29, "2024-02-29" }
            ],
            "ORDER BY `column3` ASC");
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
            FROM Calendar(table=orders, field=CreatedAt)
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
    [DisplayName("Script Calendar table/field FIRST использует alias поля исходной таблицы")]
    public async Task Execute_script_calendar_table_field_first_uses_source_field_alias_mapping()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            orders:
            LOAD
                marker,
                Date(date_text, 'yyyy-MM-dd') AS CreatedAt
            FROM Inline(date_text, marker;
                '2024-01-01', 'a';
                '2024-01-02', 'b';
                '2024-01-03', 'c');

            calendar:
            FIRST 2
            LOAD
                Date,
                DayOfMonth
            FROM Calendar(table=orders, field=CreatedAt)
            ORDER BY Date DESC;
            """);

        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[1],
            ["Date", "DayOfMonth"],
            [
                new object?[] { new DateTime(2024, 1, 2), (byte)2 },
                new object?[] { new DateTime(2024, 1, 1), (byte)1 }
            ],
            "ORDER BY `column1` DESC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }

    [Test]
    [DisplayName("Script Calendar table/field читает TEMP LOAD источник и чистит его в конце")]
    public async Task Execute_script_calendar_table_field_reads_temp_source_and_cleans_it_at_the_end()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            orders:
            TEMP LOAD
                Date(2024, 1, 1).AddDays(number) AS CreatedAt
            FROM Numbers(max=2);

            calendar:
            LOAD
                Date,
                DayOfMonth
            FROM Calendar(table=orders, field=CreatedAt)
            ORDER BY Date ASC;
            """);

        await Assert.That(execution.Tables).Count().IsEqualTo(1);
        await Assert.That(execution.Tables[0].Alias).IsEqualTo("calendar");
        await ScriptIntegrationAssert.AssertFinalTableAsync(
            database,
            execution.Tables[0],
            ["Date", "DayOfMonth"],
            [
                new object?[] { new DateTime(2024, 1, 1), (byte)1 },
                new object?[] { new DateTime(2024, 1, 2), (byte)2 },
                new object?[] { new DateTime(2024, 1, 3), (byte)3 }
            ],
            "ORDER BY `column1` ASC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
        await ScriptIntegrationAssert.AssertTableCountWithPrefixAsync(database, execution.FinalTablePrefix, 1);
    }

    [Test]
    [DisplayName("Script Calendar поддерживает positional table/field")]
    public async Task Execute_script_loads_calendar_from_positional_table_field()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            orders:
            LOAD
                Date(2024, 1, 1).AddDays(number) AS CreatedAt
            FROM Numbers(2);

            calendar:
            LOAD
                Date,
                Year,
                DayOfMonth
            FROM Calendar(orders, CreatedAt)
            ORDER BY Date ASC;
            """);

        var result = execution.Tables;
        await Assert.That(result).Count().IsEqualTo(2);
        var rows = await ScriptIntegrationAssert.ReadFinalTableAsync(database, result[1], "ORDER BY `column1` ASC");
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
            FROM Calendar(table=orders, field=CreatedAt);
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
}

using System.Globalization;
using ClickHouse.Client.ADO;
using Loader.Core.Models;
using Loader.Script.Tests.Infrastructure;
using TUnit.Assertions.Enums;

namespace Loader.Script.Tests;

[ClassDataSource<ClickHouseTestDatabase>(Shared = SharedType.PerTestSession)]
[ParallelLimiter<ClickHouseParallelLimit>]
public sealed class CalendarStatementClickHouseTests
{
    private static readonly string[] ExpectedFieldNames =
    [
        "Date", "Year", "QuarterNumber", "Quarter", "YearQuarterNumber", "YearQuarter",
        "MonthNumber", "MonthName", "MonthShortName", "YearMonthNumber", "YearMonth",
        "MonthYear", "WeekNumber", "YearWeek", "StartOfWeek", "LastDayOfWeek",
        "DayOfWeek", "DayOfWeekName", "DayOfMonth", "DayOfYear", "StartOfYear",
        "EndOfYear", "StartOfQuarter", "EndOfQuarter", "StartOfMonth", "EndOfMonth",
        "DayMonth", "WeekPeriod"
    ];

    private readonly ClickHouseTestDatabase database;

    public CalendarStatementClickHouseTests(ClickHouseTestDatabase database)
    {
        this.database = database;
    }

    [Test]
    [DisplayName("CALENDAR материализует включительный диапазон и фиксированную схему")]
    public async Task Literal_calendar_materializes_leap_day_and_fixed_schema()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            Calendar:
            CALENDAR FROM '2024-02-28' TO '2024-03-01';
            """);

        await Assert.That(execution.Tables).Count().IsEqualTo(1);
        var calendar = execution.Tables[0];
        await Assert.That(calendar.Alias).IsEqualTo("Calendar");
        await Assert.That(calendar.RowCount).IsEqualTo(3);
        await Assert.That(calendar.Fields.Select(static field => field.Name).ToArray())
            .IsEquivalentTo(ExpectedFieldNames, CollectionOrdering.Matching);
        await Assert.That(calendar.Fields).Count().IsEqualTo(28);
        await Assert.That(calendar.Fields.All(static field => !field.CanBeNull)).IsTrue();
        await Assert.That(calendar.Fields[0].DataType).IsEqualTo(DataType.Date);
        await Assert.That(calendar.Fields[1].DataType).IsEqualTo(DataType.Integer);
        await Assert.That(calendar.Fields[3].DataType).IsEqualTo(DataType.Text);

        var row = await ExecuteSingleRowAsync(
            $"""
             SELECT
                 count(),
                 toString(min(`column1`)),
                 toString(max(`column1`)),
                 anyIf(`column8`, `column1` = toDate('2024-02-29')),
                 anyIf(`column9`, `column1` = toDate('2024-02-29')),
                 anyIf(`column18`, `column1` = toDate('2024-02-29')),
                 anyIf(`column27`, `column1` = toDate('2024-02-29')),
                 anyIf(`column28`, `column1` = toDate('2024-02-29'))
             FROM {calendar.Name.ToSql()}
             """);
        await Assert.That(Convert.ToInt64(row[0], CultureInfo.InvariantCulture)).IsEqualTo(3);
        await Assert.That((string)row[1]!).IsEqualTo("2024-02-28");
        await Assert.That((string)row[2]!).IsEqualTo("2024-03-01");
        await Assert.That((string)row[3]!).IsEqualTo("Февраль");
        await Assert.That((string)row[4]!).IsEqualTo("Фев");
        await Assert.That((string)row[5]!).IsEqualTo("Чт");
        await Assert.That((string)row[6]!).IsEqualTo("29.02");
        await Assert.That((string)row[7]!).IsEqualTo("26.02-03.03");

        await AssertCalendarStorageAsync(calendar);
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }

    [Test]
    [DisplayName("CALENDAR RESIDENT берет MIN/MAX предыдущего LOAD, игнорирует NULL и заполняет пропуски")]
    public async Task Resident_calendar_uses_prior_load_min_max_and_fills_missing_days()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            Orders:
            LOAD
                Date(created_at, 'yyyy-MM-dd') AS CreatedAt
            FROM [calendar-orders.csv] (csv);

            Calendar:
            CALENDAR FROM FIELD CreatedAt RESIDENT Orders;
            """);

        await Assert.That(execution.Tables).Count().IsEqualTo(2);
        var calendar = execution.Tables[1];
        await Assert.That(calendar.RowCount).IsEqualTo(3);

        var row = await ExecuteSingleRowAsync(
            $"""
             SELECT
                 arrayStringConcat(groupArray(toString(`column1`)), ',')
             FROM
             (
                 SELECT `column1`
                 FROM {calendar.Name.ToSql()}
                 ORDER BY `column1`
             )
             """);
        await Assert.That((string)row[0]!).IsEqualTo("2024-02-28,2024-02-29,2024-03-01");

        await ScriptIntegrationAssert.AssertNoTempTablesAsync(database, execution);
    }

    [Test]
    [DisplayName("CALENDAR корректно вычисляет границы недели месяца квартала и года")]
    public async Task Calendar_calculates_period_boundaries()
    {
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            database,
            """
            Calendar:
            CALENDAR FROM '2023-12-31' TO '2024-04-01';
            """);
        var calendar = execution.Tables[0];

        var row = await ExecuteSingleRowAsync(
            $"""
             SELECT
                 toString(anyIf(`column21`, `column1` = toDate('2023-12-31'))),
                 toString(anyIf(`column22`, `column1` = toDate('2023-12-31'))),
                 toString(anyIf(`column23`, `column1` = toDate('2024-04-01'))),
                 toString(anyIf(`column24`, `column1` = toDate('2024-04-01'))),
                 toString(anyIf(`column25`, `column1` = toDate('2024-02-29'))),
                 toString(anyIf(`column26`, `column1` = toDate('2024-02-29'))),
                 toString(anyIf(`column15`, `column1` = toDate('2023-12-31'))),
                 toString(anyIf(`column16`, `column1` = toDate('2023-12-31')))
             FROM {calendar.Name.ToSql()}
             """);
        await Assert.That((string)row[0]!).IsEqualTo("2023-01-01");
        await Assert.That((string)row[1]!).IsEqualTo("2023-12-31");
        await Assert.That((string)row[2]!).IsEqualTo("2024-04-01");
        await Assert.That((string)row[3]!).IsEqualTo("2024-06-30");
        await Assert.That((string)row[4]!).IsEqualTo("2024-02-01");
        await Assert.That((string)row[5]!).IsEqualTo("2024-02-29");
        await Assert.That((string)row[6]!).IsEqualTo("2023-12-25");
        await Assert.That((string)row[7]!).IsEqualTo("2023-12-31");
    }

    private async Task AssertCalendarStorageAsync(LoadedTable calendar)
    {
        var row = await ExecuteSingleRowAsync(
            $"""
             SELECT engine, sorting_key
             FROM system.tables
             WHERE database = currentDatabase()
               AND name = '{calendar.Name.Table}'
             """);
        await Assert.That((string)row[0]!).IsEqualTo("MergeTree");
        await Assert.That((string)row[1]!).Contains("column1");
    }

    private async Task<object[]> ExecuteSingleRowAsync(string sql)
    {
        await using var connection = new ClickHouseConnection(database.ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        if (!await reader.ReadAsync().ConfigureAwait(false))
        {
            throw new InvalidOperationException("ClickHouse query returned no rows.");
        }

        var row = new object[reader.FieldCount];
        reader.GetValues(row);
        return row;
    }
}

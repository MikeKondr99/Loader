using Loader.Query.Tests.Infrastructure;

namespace Loader.Query.Tests.Functions.Date;

public sealed class ClickHouseDateFunctionTests : ClickHouseExpressionTestBase
{
    public ClickHouseDateFunctionTests(ClickHouseTestDatabase database)
        : base(database)
    {
    }

    [Test]
    [Arguments("Date('2023-01-01').AddDays(1)", "@2023-01-02 00:00")]
    [Arguments("Date('2023-01-01 08:30').AddDays(1)", "@2023-01-02 08:30")]
    [Arguments("Date('2023-12-31').AddDays(1)", "@2024-01-01 00:00")]
    [Arguments("Date('2024-02-28').AddDays(1)", "@2024-02-29 00:00")]
    [Arguments("Date('2023-01-01').AddDays(-1)", "@2022-12-31 00:00")]
    public Task Add_days(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("Date('2023-01-15').AddMonths(1)", "@2023-02-15 00:00")]
    [Arguments("Date('2023-01-31').AddMonths(1)", "@2023-02-28 00:00")]
    [Arguments("Date('2024-01-31').AddMonths(1)", "@2024-02-29 00:00")]
    [Arguments("Date('2023-12-15').AddMonths(1)", "@2024-01-15 00:00")]
    [Arguments("Date('2023-01-15').AddMonths(-1)", "@2022-12-15 00:00")]
    public Task Add_months(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("Date('2023-02-15').AddYears(1)", "@2024-02-15 00:00")]
    [Arguments("Date('2024-02-29').AddYears(1)", "@2025-02-28 00:00")]
    [Arguments("Date('2023-02-15 18:20').AddYears(1)", "@2024-02-15 18:20")]
    [Arguments("Date('2023-02-15').AddYears(-1)", "@2022-02-15 00:00")]
    public Task Add_years(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("Date('2023-05-15').Year()", 2023)]
    [Arguments("Date('2024-02-29 14:30:22').Year()", 2024)]
    [Arguments("Date('1999-12-31 23:59:59').Year()", 1999)]
    [Arguments("Date('2023-05-15').Month()", 5)]
    [Arguments("Date('2023-12-31').Month()", 12)]
    [Arguments("Date('2023-05-15').Day()", 15)]
    [Arguments("Date('2024-02-29').Day()", 29)]
    [Arguments("Date('2023-05-15 14:30:22').Hour()", 14)]
    [Arguments("Date('2023-05-15 14:30:22').Minute()", 30)]
    [Arguments("Date('2023-05-15 14:30:22').Second()", 22)]
    public Task Date_parts(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("Date('2023-01-15').Quarter()", 1)]
    [Arguments("Date('2023-04-01').Quarter()", 2)]
    [Arguments("Date('2023-07-15').Quarter()", 3)]
    [Arguments("Date('2023-10-31').Quarter()", 4)]
    [Arguments("Date('2023-05-15').YearMonth()", "2023-05")]
    [Arguments("Date('2023-01-01 14:30:22').YearMonth()", "2023-01")]
    [Arguments("Date('2023-01-15').YearQuarter()", "2023-Q1")]
    [Arguments("Date('2023-04-01').YearQuarter()", "2023-Q2")]
    public Task Date_grouping_text(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("Date('2023-01-02').YearWeek()", "2023-W01")]
    [Arguments("Date('2023-01-01').YearWeek()", "2022-W52")]
    [Arguments("Date('2020-12-31').YearWeek()", "2020-W53")]
    [Arguments("Date('2023-01-01').DayOfYear()", 1)]
    [Arguments("Date('2023-12-31').DayOfYear()", 365)]
    [Arguments("Date('2024-12-31').DayOfYear()", 366)]
    [Arguments("Date('2023-05-15').DayOfWeek()", 1)]
    [Arguments("Date('2023-05-21').DayOfWeek()", 7)]
    [Arguments("Date('2023-01-02').Week()", 1)]
    [Arguments("Date('2023-01-01').Week()", 52)]
    public Task Calendar_fields(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("Date(2023)", "@2023-01-01 00:00:00")]
    [Arguments("Date(1970)", "@1970-01-01 00:00:00")]
    [Arguments("Date(2023, 5)", "@2023-05-01 00:00:00")]
    [Arguments("Date(2024, 2)", "@2024-02-01 00:00:00")]
    [Arguments("Date(2023, 5, 15)", "@2023-05-15 00:00:00")]
    [Arguments("Date(2024, 2, 29)", "@2024-02-29 00:00:00")]
    public Task Date_constructors(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("Now().Year() >= 1970", true)]
    [Arguments("Today().Hour()", 0)]
    [Arguments("Today().Minute()", 0)]
    [Arguments("Today().Second()", 0)]
    public Task Current_date_functions(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }
}

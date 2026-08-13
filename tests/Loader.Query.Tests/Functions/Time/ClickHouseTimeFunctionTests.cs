using Loader.Lang.Expressions;
using Loader.Query.Functions;
using Loader.Query.Models;
using Loader.Query.Resolve;
using Loader.Query.Tests.Infrastructure;

namespace Loader.Query.Tests.Functions.Time;

public sealed class ClickHouseTimeFunctionTests : ClickHouseExpressionTestBase
{
    public ClickHouseTimeFunctionTests(ClickHouseTestDatabase database)
        : base(database)
    {
    }

    [Test]
    [Arguments("Time('03:04:05')", "@1970-01-01 03:04:05")]
    [Arguments("'23:59:59'.Time()", "@1970-01-01 23:59:59")]
    [Arguments("Time('bad')", null)]
    [Arguments("Time('03:04')", null)]
    [Arguments("Time('24:00:00')", null)]
    [Arguments("Time('25:00:00')", null)]
    [Arguments("Time(null)", null)]
    [DisplayName("Time парсит текст в time-only формате HH:mm:ss")]
    public Task Time_parse(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("Time('03.04.05', 'HH.mm.ss')", "@1970-01-01 03:04:05")]
    [Arguments("'03:04:05 PM'.Time('hh:mm:ss a')", "@1970-01-01 15:04:05")]
    [Arguments("'13:04:05 PM'.Time('hh:mm:ss a')", null)]
    [Arguments("'18 PM'.Time('hh a')", null)]
    [Arguments("Time('2026-01-02 03:04:05', 'yyyy-MM-dd HH:mm:ss')", "@1970-01-01 03:04:05")]
    [Arguments("Time('2026-01-02', 'yyyy-MM-dd')", "@1970-01-01 00:00:00")]
    [Arguments("Time('bad', 'HH.mm.ss')", null)]
    [Arguments("Time(null, 'HH.mm.ss')", null)]
    [DisplayName("Time с format парсит текст по Joda time-only format")]
    public Task Time_parse_with_format(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("Time(3, 4)", "@1970-01-01 03:04:00")]
    [Arguments("Time(3, 4, 5)", "@1970-01-01 03:04:05")]
    [Arguments("Time(23, 59, 59)", "@1970-01-01 23:59:59")]
    [Arguments("Time(24, 0, 0)", null)]
    [Arguments("Time(3, 60, 0)", null)]
    [Arguments("Time(3, 4, 60)", null)]
    [Arguments("Time(null, 4, 5)", null)]
    [DisplayName("Time создает время из числовых компонентов")]
    public Task Time_from_parts(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("Time('03:04:05').Text()", "03:04:05")]
    [Arguments("Time('15:04:05').Text('hh:mm:ss a')", "03:04:05 PM")]
    [Arguments("Time('18:00:00').Text('hh a')", "06 PM")]
    [Arguments("Time('18:00:00').Text('yyyy-MM-dd HH:mm:ss')", "1970-01-01 18:00:00")]
    [Arguments("Time(null).Text()", null)]
    [DisplayName("Text форматирует Time обратно в текст")]
    public Task Text_time(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("Time('03:04:05').Hour()", 3)]
    [Arguments("Time('03:04:05').Minute()", 4)]
    [Arguments("Time('03:04:05').Second()", 5)]
    [Arguments("Time(null).Hour()", null)]
    [DisplayName("Hour Minute Second возвращают компоненты Time")]
    public Task Time_parts(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("Type(Time('03:04:05'))", "time")]
    [Arguments("RawType(Time('03:04:05'))", "Nullable(DateTime('UTC'))")]
    [Arguments("RawType(Time('bad'))", "Nullable(DateTime('UTC'))")]
    [DisplayName("Time имеет логический тип time и физический ClickHouse DateTime")]
    public Task Time_types(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [DisplayName("Time format должен быть константой")]
    public async Task Time_format_must_be_constant()
    {
        var source = InlineQueryArrange.SingleColumnSource(
            "Format",
            DataType.Text,
            ["'HH.mm.ss'"],
            canBeNull: false);
        var query = new Query.Models.Query
        {
            Source = source,
            Select =
            [
                new SelectItem
                {
                    Alias = "value",
                    Expression = Expr.Parse("Time('03.04.05', Format)").Value
                }
            ]
        };

        var result = new QueryResolver().Resolve(query, ClickHouseFunctions.CreateResolver());

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Errors.Select(static error => error.Message).First())
            .Contains("Time");
    }
}

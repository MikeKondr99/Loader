using Loader.Query.Tests.Infrastructure;

namespace Loader.Query.Tests.Functions.Comparison;

public sealed class ClickHouseComparisonFunctionTests : ClickHouseExpressionTestBase
{
    public ClickHouseComparisonFunctionTests(ClickHouseTestDatabase database)
        : base(database)
    {
    }

    [Test]
    [Arguments("5 < 10", true)]
    [Arguments("10 < 5", false)]
    [Arguments("1.5 < 1.3", false)]
    [Arguments("1.3 < 1.5", true)]
    [Arguments("-4.0 < -2.0", true)]
    [Arguments("Date('2026-01-01') < Date('2026-01-02')", true)]
    [Arguments("Date('2026-01-02') < Date('2026-01-01')", false)]
    [Arguments("5 < Int(null)", null)]
    public Task Less_than(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("5 > 10", false)]
    [Arguments("10 > 5", true)]
    [Arguments("1.5 > 1.3", true)]
    [Arguments("1.3 > 1.5", false)]
    [Arguments("-2.0 > -4.0", true)]
    [Arguments("Date('2026-01-02') > Date('2026-01-01')", true)]
    [Arguments("Date('2026-01-01') > Date('2026-01-02')", false)]
    [Arguments("5 > Int(null)", null)]
    public Task Greater_than(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("5 <= 10", true)]
    [Arguments("10 <= 5", false)]
    [Arguments("1.5 <= 1.3", false)]
    [Arguments("1.3 <= 1.5", true)]
    [Arguments("1.5 <= 1.5", true)]
    [Arguments("-4.0 <= -2.0", true)]
    [Arguments("Date('2026-01-01') <= Date('2026-01-02')", true)]
    [Arguments("Date('2026-01-02') <= Date('2026-01-01')", false)]
    [Arguments("Date('2026-01-02') <= Date('2026-01-02')", true)]
    [Arguments("5 <= Int(null)", null)]
    public Task Less_than_or_equal(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("5 >= 10", false)]
    [Arguments("10 >= 5", true)]
    [Arguments("1.5 >= 1.3", true)]
    [Arguments("1.3 >= 1.5", false)]
    [Arguments("1.5 >= 1.5", true)]
    [Arguments("-2.0 >= -4.0", true)]
    [Arguments("Date('2026-01-02') >= Date('2026-01-01')", true)]
    [Arguments("Date('2026-01-01') >= Date('2026-01-02')", false)]
    [Arguments("Date('2026-01-02') >= Date('2026-01-02')", true)]
    [Arguments("5 >= Int(null)", null)]
    public Task Greater_than_or_equal(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("5 = 5", true)]
    [Arguments("5 = 10", false)]
    [Arguments("1.5 = 1.5", true)]
    [Arguments("1.5 = 1.3", false)]
    [Arguments("-4.0 = -4.0", true)]
    [Arguments("'abc' = 'abc'", true)]
    [Arguments("'abc' = 'ABC'", false)]
    [Arguments("Date('2026-01-01') = Date('2026-01-01')", true)]
    [Arguments("Date('2026-01-01') = Date('2026-01-02')", false)]
    [Arguments("5 = Int(null)", null)]
    [Arguments("Text(null) = Text(null)", null)]
    public Task Equal_to(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("5 != 10", true)]
    [Arguments("5 != 5", false)]
    [Arguments("1.5 != 1.3", true)]
    [Arguments("1.5 != 1.5", false)]
    [Arguments("-4.0 != -2.0", true)]
    [Arguments("'abc' != 'ABC'", true)]
    [Arguments("'abc' != 'abc'", false)]
    [Arguments("Date('2026-01-01') != Date('2026-01-02')", true)]
    [Arguments("Date('2026-01-01') != Date('2026-01-01')", false)]
    [Arguments("5 != Int(null)", null)]
    [Arguments("Text(null) != Text(null)", null)]
    public Task Not_equal_to(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("5.Between(1, 10)", true)]
    [Arguments("15.Between(1, 10)", false)]
    [Arguments("Between(5, 1, 10)", true)]
    [Arguments("Between(15, 1, 10)", false)]
    [Arguments("1.5.Between(1.0, 2.0)", true)]
    [Arguments("0.9.Between(1.0, 2.0)", false)]
    [Arguments("Between(1.5, 1.0, 2.0)", true)]
    [Arguments("Between(0.9, 1.0, 2.0)", false)]
    [Arguments("1.Between(1, 10)", true)]
    [Arguments("10.Between(1, 10)", true)]
    [Arguments("0.Between(1, 10)", false)]
    [Arguments("11.Between(1, 10)", false)]
    [Arguments("5.Between(1.5, 10.5)", true)]
    [Arguments("Between(5.5, 1, 10)", true)]
    [Arguments("Date('2026-01-02').Between(Date('2026-01-01'), Date('2026-01-03'))", true)]
    [Arguments("Date('2026-01-04').Between(Date('2026-01-01'), Date('2026-01-03'))", false)]
    [Arguments("Date('2026-01-01').Between(Date('2026-01-01'), Date('2026-01-03'))", true)]
    [Arguments("Date('2026-01-03').Between(Date('2026-01-01'), Date('2026-01-03'))", true)]
    [Arguments("Int(null).Between(1, 10)", null)]
    [Arguments("5.Between(Int(null), 10)", null)]
    [Arguments("5.Between(1, Int(null))", null)]
    public Task Between_function(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }
}

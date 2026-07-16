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
    public Task Between_function(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }
}

using Loader.Query.Tests.Infrastructure;

namespace Loader.Query.Tests.Functions.Math;

public sealed class ClickHouseMathFunctionTests : ClickHouseExpressionTestBase
{
    public ClickHouseMathFunctionTests(ClickHouseTestDatabase database)
        : base(database)
    {
    }

    [Test]
    [Arguments("2 + 2", 4)]
    [Arguments("2.5 + 3.5", 6.0)]
    [Arguments("2.5 + 4", 6.5)]
    [Arguments("Type(3 + 3)", "int!")]
    [Arguments("2 + null", null)]
    [Arguments("Type(2 + null)", "int")]
    public Task Add(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("3 - 8", -5)]
    [Arguments("3.0 - 8.5", -5.5)]
    public Task Subtract(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("-4", -4)]
    [Arguments("-3.0", -3.0)]
    [Arguments("-0.0", 0.0)]
    [Arguments("-0", 0)]
    public Task Unary_minus(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("-4.0 * 8.0", -32.0)]
    [Arguments("-8 * -3", 24)]
    public Task Multiply(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("10 / 2", 5)]
    [Arguments("10 / 6", 1)]
    [Arguments("-5 / 2", -2)]
    [Arguments("5 / -2", -2)]
    [Arguments("-5 / -2", 2)]
    [Arguments("If((100 / 3) > 33.2, 'fail', 'success')", "success")]
    [Arguments("If((10 / 3) > 3.4, 1, 0)", 0)]
    [Arguments("If((-5 / 2) > -2.5, 1, 0)", 1)]
    [Arguments("10.0 / 4.0", 2.5)]
    [Arguments("1 / 0", null)]
    [Arguments("1.0 / 0.0", null)]
    [Arguments("If((1 / 0) > 1, 1, 0)", 0)]
    [Arguments("If((1.0 / 0.0) > 1.0, 1, 0)", 0)]
    public Task Divide(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("2 + 2 * 2", 6)]
    [Arguments("(2 + 2) * 2", 8)]
    [Arguments("(-10 + 1).Sign()", -1)]
    [Arguments("-10 + 1.Sign()", -9)]
    public Task Priority(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("E()", 2.718281828459045)]
    [Arguments("E() > 2.71", true)]
    [Arguments("E() < 2.72", true)]
    [Arguments("E() + 1", 3.718281828459045)]
    [Arguments("E() * 2", 5.43656365691809)]
    [Arguments("E() / 2", 1.3591409142295225)]
    public Task E_function(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("Pi()", 3.141592653589793)]
    [Arguments("Pi() > 3.14", true)]
    [Arguments("Pi() < 3.15", true)]
    [Arguments("Pi() + 1", 4.141592653589793)]
    [Arguments("Pi() * 2", 6.283185307179586)]
    [Arguments("Pi() / 2", 1.5707963267948966)]
    public Task Pi_function(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }
}

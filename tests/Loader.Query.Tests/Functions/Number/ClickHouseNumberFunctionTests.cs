using Loader.Query.Tests.Infrastructure;

namespace Loader.Query.Tests.Functions.Number;

public sealed class ClickHouseNumberFunctionTests : ClickHouseExpressionTestBase
{
    public ClickHouseNumberFunctionTests(ClickHouseTestDatabase database)
        : base(database)
    {
    }

    [Test]
    [Arguments("Floor(1.2)", 1.0)]
    [Arguments("Floor(1.8)", 1.0)]
    [Arguments("Floor(4.7, 2.0)", 4.0)]
    [Arguments("Floor(2.4)", 2.0)]
    [Arguments("Floor(4.2)", 4.0)]
    [Arguments("Floor(3.88, .1)", 3.8)]
    [Arguments("Floor(3.88, 5.0)", 0.0)]
    [Arguments("Floor(1.1, 1.0)", 1.0)]
    [Arguments("Floor(4.7, .5)", 4.5)]
    [Arguments("Floor(1.1, 1.0, 0.5)", 0.5)]
    [Arguments("Floor(-150.0, 50.0, 25.0)", -175.0)]
    public Task Floor(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("Ceil(1.2)", 2.0)]
    [Arguments("Ceil(1.8)", 2.0)]
    [Arguments("Ceil(4.7, .5)", 5.0)]
    [Arguments("Ceil(4.7, 2.0)", 6.0)]
    [Arguments("Ceil(1.1, 1.0, -0.01)", 1.99)]
    public Task Ceil(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("Round(1.2)", 1.0)]
    [Arguments("Round(1.8)", 2.0)]
    [Arguments("Round(0.5)", 1.0)]
    [Arguments("Round(0.7)", 1.0)]
    [Arguments("Round(4.7, 2.0)", 4.0)]
    [Arguments("Round(5.3, 2.0)", 6.0)]
    [Arguments("Round(4.7, .5)", 4.5)]
    [Arguments("Round(5.3, .5)", 5.5)]
    [Arguments("Round(2.5, 1.0)", 3.0)]
    [Arguments("Round(2.0, 4.0)", 4.0)]
    [Arguments("Round(1.1, 1.0, .5)", 1.5)]
    [Arguments("Round(2.0, 4.0, .0)", 4.0)]
    [Arguments("Round(100.0, 1.0, 200.0)", 100.0)]
    [Arguments("Round(-130.0, 50.0, 25.0)", -125.0)]
    public Task Round(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("Mod(7, 2)", 1)]
    [Arguments("Mod(9, 3)", 0)]
    [Arguments("Mod(-4, 3)", -1)]
    [Arguments("Mod(4, -3)", 1)]
    [Arguments("Mod(-4, -3)", -1)]
    [Arguments("If(Mod(10, 3) > 1.4, 1, 0)", 0)]
    public Task Mod(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("Rem(10, 5)", 0)]
    [Arguments("Rem(6, 5)", 1)]
    [Arguments("Rem(-9, 5)", 4)]
    [Arguments("Rem(0, 5)", 0)]
    [Arguments("Rem(10, -3)", 1)]
    [Arguments("If(Rem(-10, 3) > 1.4, 1, 0)", 0)]
    public Task Rem(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("Abs(1.5)", 1.5)]
    [Arguments("Abs(.0 - 1.5)", 1.5)]
    [Arguments("Abs(3)", 3)]
    [Arguments("Abs(-3)", 3)]
    [Arguments("Abs(null)", null)]
    public Task Abs(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("Even(0)", true)]
    [Arguments("Even(1)", false)]
    [Arguments("Even(2)", true)]
    [Arguments("Even(-1)", false)]
    [Arguments("Even(-2)", true)]
    [Arguments("Odd(0)", false)]
    [Arguments("Odd(1)", true)]
    [Arguments("Odd(2)", false)]
    [Arguments("Odd(-1)", true)]
    [Arguments("Odd(-2)", false)]
    public Task Even_odd(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("Sign(0)", 0)]
    [Arguments("Sign(2)", 1)]
    [Arguments("Sign(-2)", -1)]
    [Arguments("Sign(0.0)", 0)]
    [Arguments("Sign(2.1)", 1)]
    [Arguments("(-2.1).Sign()", -1)]
    [Arguments("If(Sign(2.1) > 1.4, 1, 0)", 0)]
    [Arguments("Sign(null)", null)]
    public Task Sign(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("Frac(0.0)", 0.0)]
    [Arguments("Frac(10.0)", 0.0)]
    [Arguments("Frac(1.123)", 0.123)]
    [Arguments("Frac(-2.123456789)", -0.123456789)]
    public Task Frac(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }
}

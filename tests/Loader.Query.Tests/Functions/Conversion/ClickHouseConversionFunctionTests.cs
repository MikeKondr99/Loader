using Loader.Query.Tests.Infrastructure;

namespace Loader.Query.Tests.Functions.Conversion;

public sealed class ClickHouseConversionFunctionTests : ClickHouseExpressionTestBase
{
    public ClickHouseConversionFunctionTests(ClickHouseTestDatabase database)
        : base(database)
    {
    }

    [Test]
    [Arguments("Int(25)", 25)]
    [Arguments("Int('25')", 25)]
    [Arguments("Int(2.6)", 2)]
    [Arguments("If(Int(3.9) > 3.4, 1, 0)", 0)]
    [Arguments("Int(false)", 0)]
    [Arguments("Int(true)", 1)]
    [Arguments("Int(null)", null)]
    public Task Int(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("Num(25)", 25.0)]
    [Arguments("Num('25')", 25.0)]
    [Arguments("Num(2.5)", 2.5)]
    [Arguments("Num(false)", 0.0)]
    [Arguments("Num(true)", 1.0)]
    [Arguments("Num(null)", null)]
    public Task Num(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("Text(25)", "25")]
    [Arguments("Text(25000000)", "25000000")]
    [Arguments("Text('25')", "25")]
    [Arguments("Text(2.5)", "2.5")]
    [Arguments("Text(1000000.123)", "1000000.123")]
    [Arguments("Text(false)", "false")]
    [Arguments("Text(true)", "true")]
    [Arguments("Text(null)", null)]
    [Arguments("Date('2025-03-27 21:19').Text()", "2025-03-27 21:19:00")]
    [Arguments("Date('2025-03-27').Text()", "2025-03-27 00:00:00")]
    public Task Text(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("Bool(25)", true)]
    [Arguments("Bool(0)", false)]
    [Arguments("Bool(-5)", false)]
    [Arguments("Bool(23)", true)]
    [Arguments("Bool(0.0)", false)]
    [Arguments("Bool(-5.0)", false)]
    [Arguments("Bool(23.0)", true)]
    [Arguments("Bool('25')", true)]
    [Arguments("Bool('')", false)]
    [Arguments("Bool(false)", false)]
    [Arguments("Bool(true)", true)]
    [Arguments("If(Bool(null), 'then', 'else')", "else")]
    public Task Bool(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("Date('2025-03-27 21:19')", "@2025-03-27 21:19")]
    [Arguments("'2025-03-27 21:40'.Date()", "@2025-03-27 21:40")]
    [Arguments("Date('2025-03-27')", "@2025-03-27 00:00")]
    [Arguments("''.EmptyIsNull().Date()", null)]
    [Arguments("Date('2025-03-27').Date().Date()", "@2025-03-27 00:00")]
    public Task Date(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }
}

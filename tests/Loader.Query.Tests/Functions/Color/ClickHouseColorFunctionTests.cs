using Loader.Query.Tests.Infrastructure;

namespace Loader.Query.Tests.Functions.Color;

public sealed class ClickHouseColorFunctionTests : ClickHouseExpressionTestBase
{
    public ClickHouseColorFunctionTests(ClickHouseTestDatabase database)
        : base(database)
    {
    }

    [Test]
    [Arguments("Rgb(0, 0, 0)", 0xFF000000L)]
    [Arguments("Rgb(255, 255, 255)", 0xFFFFFFFFL)]
    [Arguments("Rgb(255, 0, 0)", 0xFFFF0000L)]
    [Arguments("Rgb(0, 255, 0)", 0xFF00FF00L)]
    [Arguments("Rgb(0, 0, 255)", 0xFF0000FFL)]
    [Arguments("Rgb(1, 2, 3)", 0xFF010203L)]
    [Arguments("Rgb(257, 2, 3)", 0xFF010203L)]
    [Arguments("Rgb(-1, 0, 0)", 0xFFFF0000L)]
    [Arguments("Rgb(0, -1, 0)", 0xFF00FF00L)]
    [Arguments("Rgb(0, 0, -1)", 0xFF0000FFL)]
    [Arguments("Rgb(null, 0, 0)", null)]
    [Arguments("Rgb(0, null, 0)", null)]
    [Arguments("Rgb(0, 0, null)", null)]
    [Arguments("Type(Rgb(1, 2, 3))", "int!")]
    public Task Rgb(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }
}

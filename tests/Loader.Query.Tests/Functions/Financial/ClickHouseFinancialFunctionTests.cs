using Loader.Query.Tests.Infrastructure;

namespace Loader.Query.Tests.Functions.Financial;

public sealed class ClickHouseFinancialFunctionTests : ClickHouseExpressionTestBase
{
    public ClickHouseFinancialFunctionTests(ClickHouseTestDatabase database)
        : base(database)
    {
    }

    [Test]
    [Arguments("FutureValue(0.005, 36, -20.0).Text().Substring(1, 9)", "786.72209")]
    [Arguments("FutureValue(null, 36, -20.0)", null)]
    [Arguments("FutureValue(0.005, null, -20.0)", null)]
    [Arguments("FutureValue(0.005, 36, null)", null)]
    public Task Future_value(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }
}

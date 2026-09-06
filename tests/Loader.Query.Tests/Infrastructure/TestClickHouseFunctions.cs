using Loader.Query.Functions;
using Loader.Query.Models;
using Loader.Query.Resolve;

namespace Loader.Query.Tests.Infrastructure;

internal static class TestClickHouseFunctions
{
    public static IFunctionResolver CreateResolver()
    {
        return new FunctionStorage(
        [
            ..ClickHouseFunctions.All,
            ..new TestDecimalFunctions().GetFunctions()
        ]);
    }

    private sealed class TestDecimalFunctions : FunctionDescriptor
    {
        protected override void DefineFunctions()
        {
            Function("TestDec")
                .Doc("Тестовый helper для стабильного получения ClickHouse Decimal(P, S)")
                .Arg("value", DataType.Number)
                .ConstArg("precision", DataType.Integer)
                .ConstArg("scale", DataType.Integer)
                .Returns(DataType.Number)
                .Template($"CAST({0} AS Decimal({1}, {2}))");
        }
    }
}

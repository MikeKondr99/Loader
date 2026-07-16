using Loader.Query.Models;

namespace Loader.Query.Functions;

public sealed class FinancialFunctions : FunctionDescriptor
{
    protected override void DefineFunctions()
    {
        Function("FutureValue")
            .Doc("Вычисляет будущую стоимость инвестиций на основе постоянной процентной ставки, количества периодов и постоянных платежей")
            .Arg("rate", DataType.Number)
            .Arg("nper", DataType.Integer)
            .Arg("pmt", DataType.Number)
            .Returns(DataType.Number)
            .Template($"(-1 * {2} * (1 - POWER(1 + {0}, -{1})) / {0}) * POWER(1 + {0}, {1})");
    }
}

using Loader.Query.Models;

namespace Loader.Query.Functions;

public sealed class LogicFunctions : FunctionDescriptor
{
    protected override void DefineFunctions()
    {
        Binary("and", DataType.Boolean, DataType.Boolean)
            .Doc("Логическое И")
            .Returns(DataType.Boolean)
            .Template($"({0} AND {1})");

        Binary("or", DataType.Boolean, DataType.Boolean)
            .Doc("Логическое ИЛИ")
            .Returns(DataType.Boolean)
            .Template($"({0} OR {1})");

        Method("Not")
            .Doc("Логическое отрицание")
            .Arg("input", DataType.Boolean)
            .Returns(DataType.Boolean)
            .Template($"(NOT {0})");
    }
}

using Loader.Query.Models;
using Loader.Query.Resolve;

namespace Loader.Query.Functions;

public sealed class ReflectionFunctions : FunctionDescriptor
{
    protected override void DefineFunctions()
    {
        Function("DbName")
            .Doc("Возвращает название текущей используемой внутри базы данных")
            .ReturnsNotNull(DataType.Text, ConstPropagation.AlwaysTrue)
            .Template("'ClickHouse'");

        Function("DbVersion")
            .Doc("Возвращает версию текущей используемой внутри базы данных")
            .Returns(DataType.Text)
            .Template("version()");

        Method("RawType")
            .Doc("Возвращает исходный тип выражения в ClickHouse")
            .Arg("input", DataType.Unknown, propagateNull: false)
            .ReturnsNotNull(DataType.Text, ConstPropagation.AlwaysTrue)
            .Template($"toTypeName({0})");
    }
}

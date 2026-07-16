using Loader.Query.Models;

namespace Loader.Query.Functions;

public sealed class ImplicitConversionFunctions : FunctionDescriptor
{
    protected override void DefineFunctions()
    {
        Function("ToNum")
            .Doc("Неявно преобразует целое число в дробное")
            .ImplicitCast(3)
            .ReqArg("input", DataType.Integer)
            .ReturnsNotNull(DataType.Number)
            .Template($"toFloat64({0})");

        Function("ToNum2")
            .Doc("Direct implicit cast from required integer to nullable number")
            .ImplicitCast(3)
            .ReqArg("input", DataType.Integer)
            .Returns(DataType.Number)
            .Template($"toFloat64({0})");

        Function("ToNum")
            .Doc("Неявно преобразует целое число в дробное")
            .ImplicitCast(3)
            .Arg("input", DataType.Integer)
            .Returns(DataType.Number)
            .Template($"toFloat64({0})");

        foreach (var type in new[]
                 {
                     DataType.Integer,
                     DataType.Number,
                     DataType.Boolean,
                     DataType.Text,
                     DataType.DateTime
                 })
        {
            Function("Optional")
                .Doc("Неявно расширяет not-null значение до nullable типа")
                .ImplicitCast(1)
                .ReqArg("input", type)
                .Returns(type)
                .CustomNullPropagation(_ => true)
                .Template($"{0}");
        }
    }
}

using Loader.Query.Models;

namespace Loader.Query.Functions;

public sealed class MathFunctions : FunctionDescriptor
{
    protected override void DefineFunctions()
    {
        foreach (var type in new[]
                 {
                     DataType.Integer,
                     DataType.Number
                 })
        {
            Binary("+", type, type)
                .Doc("Сложение двух чисел")
                .Returns(type)
                .Template($"({0} + {1})");

            Binary("-", type, type)
                .Doc("Вычитание двух чисел")
                .Returns(type)
                .Template($"({0} - {1})");

            Unary("-")
                .Doc("Унарный минус числа")
                .Arg("value", type)
                .Returns(type)
                .Template($"(-{0})");

            Binary("*", type, type)
                .Doc("Умножение двух чисел")
                .Returns(type)
                .Template($"({0} * {1})");
        }

        Binary("/", DataType.Number, DataType.Number)
            .Doc("Деление дробных чисел")
            .Returns(DataType.Number)
            .CustomNullPropagation(_ => true)
            .Template($"({0} / nullIf({1}, 0))");

        Binary("/", DataType.Integer, DataType.Integer)
            .Doc("Целочисленное деление")
            .Returns(DataType.Integer)
            .CustomNullPropagation(_ => true)
            .Template($"intDiv({0}, nullIf({1}, 0))");

        Method("Pow")
            .Doc("Возведение числа в степень")
            .Arg("left", DataType.Number)
            .Arg("right", DataType.Number)
            .Returns(DataType.Number)
            .Template($"POWER({0}, {1})");

        Binary("^", DataType.Number, DataType.Number)
            .Doc("Возведение числа в степень")
            .Returns(DataType.Number)
            .Template($"POWER({0}, {1})");

        Function("E")
            .Doc("Возвращает математическую константу e")
            .Returns(DataType.Number)
            .Template("2.718281828459045");

        Function("Pi")
            .Doc("Возвращает математическую константу pi")
            .Returns(DataType.Number)
            .Template("3.141592653589793");
    }
}

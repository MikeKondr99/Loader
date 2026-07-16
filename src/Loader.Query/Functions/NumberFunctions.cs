using Loader.Query.Models;

namespace Loader.Query.Functions;

public sealed class NumberFunctions : FunctionDescriptor
{
    protected override void DefineFunctions()
    {
        Method("Mod")
            .Doc("Возвращает остаток от деления")
            .Arg("input", DataType.Integer)
            .ReqArg("modulus", DataType.Integer)
            .Returns(DataType.Integer)
            .Template($"MOD({0}, {1})");

        Method("Rem")
            .Doc("Возвращает остаток от деления со знаком делителя")
            .Arg("input", DataType.Integer)
            .Arg("modulus", DataType.Integer)
            .Returns(DataType.Integer)
            .Template($"ABS(MOD({0}, {1}))");

        foreach (var type in new[]
                 {
                     DataType.Integer,
                     DataType.Number
                 })
        {
            Method("Abs")
                .Doc("Возвращает абсолютное значение")
                .Arg("input", type)
                .Returns(type)
                .Template($"ABS({0})");

            Method("Sign")
                .Doc("Возвращает знак числа")
                .Arg("input", type)
                .Returns(DataType.Integer)
                .Template($"SIGN({0})");
        }

        Method("Floor")
            .Doc("Округляет число вниз до ближайшего целого")
            .Arg("input", DataType.Number)
            .Returns(DataType.Number)
            .Template($"FLOOR({0})");

        Method("Ceil")
            .Doc("Округляет число вверх до ближайшего целого")
            .Arg("input", DataType.Number)
            .Returns(DataType.Number)
            .Template($"CEILING({0})");

        Method("Round")
            .Doc("Округляет число до ближайшего целого")
            .Arg("input", DataType.Number)
            .Returns(DataType.Number)
            .Template($"ROUND(CAST({0}, 'Decimal64(6)'), 0)");

        Method("Floor")
            .Doc("Округляет число вниз с заданным шагом")
            .Arg("input", DataType.Number)
            .Arg("step", DataType.Number)
            .Returns(DataType.Number)
            .Template($"FLOOR({0} / {1}) * {1}");

        Method("Ceil")
            .Doc("Округляет число вверх с заданным шагом")
            .Arg("input", DataType.Number)
            .Arg("step", DataType.Number)
            .Returns(DataType.Number)
            .Template($"CEILING({0} / {1}) * {1}");

        Method("Round")
            .Doc("Округляет число до ближайшего кратного заданному шагу")
            .Arg("input", DataType.Number)
            .Arg("step", DataType.Number)
            .Returns(DataType.Number)
            .Template($"ROUND(CAST({0} / {1}, 'Decimal64(6)')) * {1}");

        Method("Floor")
            .Doc("Округляет число вниз с заданным шагом и смещением")
            .Arg("input", DataType.Number)
            .Arg("step", DataType.Number)
            .Arg("offset", DataType.Number)
            .Returns(DataType.Number)
            .Template($"FLOOR(({0} - {2}) / {1}) * {1} + {2}");

        Method("Ceil")
            .Doc("Округляет число вверх с заданным шагом и смещением")
            .Arg("input", DataType.Number)
            .Arg("step", DataType.Number)
            .Arg("offset", DataType.Number)
            .Returns(DataType.Number)
            .Template($"CEILING(({0} - {2}) / {1}) * {1} + {2}");

        Method("Round")
            .Doc("Округляет число с заданным шагом и смещением")
            .Arg("input", DataType.Number)
            .Arg("step", DataType.Number)
            .Arg("offset", DataType.Number)
            .Returns(DataType.Number)
            .Template($"ROUND(CAST(({0} - {2}) / {1}, 'Decimal64(6)')) * {1} + {2}");

        Method("Even")
            .Doc("Проверяет, является ли число четным")
            .Arg("input", DataType.Integer)
            .Returns(DataType.Boolean)
            .Template($"(MOD({0}, 2) = 0)");

        Method("Odd")
            .Doc("Проверяет, является ли число нечетным")
            .Arg("input", DataType.Integer)
            .Returns(DataType.Boolean)
            .Template($"(MOD({0}, 2) <> 0)");

        Method("Frac")
            .Doc("Возвращает дробную часть числа")
            .Arg("input", DataType.Number)
            .Returns(DataType.Number)
            .Template($"MOD({0}, 1)");
    }
}

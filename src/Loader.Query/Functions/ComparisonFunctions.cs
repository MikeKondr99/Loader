using Loader.Query.Models;

namespace Loader.Query.Functions;

public sealed class ComparisonFunctions : FunctionDescriptor
{
    protected override void DefineFunctions()
    {
        foreach (var type in new[]
                 {
                     DataType.Integer,
                     DataType.Number,
                     DataType.Text,
                     DataType.DateTime
                 })
        {
            Binary("=", type, type)
                .Doc("Проверяет равенство двух значений")
                .Returns(DataType.Boolean)
                .Template($"({0} = {1})");

            Binary("!=", type, type)
                .Doc("Проверяет неравенство двух значений")
                .Returns(DataType.Boolean)
                .Template($"({0} <> {1})");
        }

        foreach (var type in new[]
                 {
                     DataType.Integer,
                     DataType.Number,
                     DataType.DateTime
                 })
        {
            Binary("<", type, type)
                .Doc("Проверяет, что первое значение строго меньше второго")
                .Returns(DataType.Boolean)
                .Template($"({0} < {1})");

            Binary(">", type, type)
                .Doc("Проверяет, что первое значение строго больше второго")
                .Returns(DataType.Boolean)
                .Template($"({0} > {1})");

            Binary("<=", type, type)
                .Doc("Проверяет, что первое значение меньше или равно второму")
                .Returns(DataType.Boolean)
                .Template($"({0} <= {1})");

            Binary(">=", type, type)
                .Doc("Проверяет, что первое значение больше или равно второму")
                .Returns(DataType.Boolean)
                .Template($"({0} >= {1})");

            Method("Between")
                .Doc("Проверяет, что значение находится в диапазоне [min, max] включительно")
                .Arg("input", type)
                .Arg("min", type)
                .Arg("max", type)
                .Returns(DataType.Boolean)
                .Template($"({0} BETWEEN {1} AND {2})");
        }
    }
}

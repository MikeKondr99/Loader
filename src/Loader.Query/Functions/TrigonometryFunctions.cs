using Loader.Query.Models;

namespace Loader.Query.Functions;

public sealed class TrigonometryFunctions : FunctionDescriptor
{
    protected override void DefineFunctions()
    {
        Method("Cos")
            .Doc("Вычисляет косинус угла")
            .Arg("radian", DataType.Number)
            .Returns(DataType.Number)
            .Template($"cos({0})");
            
        Method("Sin")
            .Doc("Вычисляет синус угла")
            .Arg("radian", DataType.Number)
            .Returns(DataType.Number)
            .Template($"sin({0})");
            
        Method("Tan")
            .Doc("Вычисляет тангенс угла")
            .Arg("radian", DataType.Number)
            .Returns(DataType.Number)
            .Template($"tan({0})");
            
        Method("Atan")
            .Doc("Вычисляет арктангенс")
            .Arg("value", DataType.Number)
            .Returns(DataType.Number)
            .Template($"atan({0})");
            
        Method("Cosh")
            .Doc("Вычисляет гиперболический косинус")
            .Arg("value", DataType.Number)
            .Returns(DataType.Number)
            .Template($"cosh({0})");
            
        Method("Sinh")
            .Doc("Вычисляет гиперболический синус")
            .Arg("value", DataType.Number)
            .Returns(DataType.Number)
            .Template($"sinh({0})");
            
        Method("Tanh")
            .Doc("Вычисляет гиперболический тангенс")
            .Arg("value", DataType.Number)
            .Returns(DataType.Number)
            .Template($"tanh({0})");
            
        Method("Asinh")
            .Doc("Вычисляет гиперболический арксинус")
            .Arg("value", DataType.Number)
            .Returns(DataType.Number)
            .Template($"asinh({0})");

        Method("Acos")
            .Doc("Вычисляет арккосинус угла")
            .Arg("value", DataType.Number)
            .Returns(DataType.Number)
            .CustomNullPropagation(_ => true)
            .Template($"CASE WHEN isNaN(acos({0})) THEN NULL ELSE acos({0}) END");

        Method("Asin")
            .Doc("Вычисляет арксинус угла")
            .Arg("value", DataType.Number)
            .Returns(DataType.Number)
            .CustomNullPropagation(_ => true)
            .Template($"CASE WHEN isNaN(asin({0})) THEN NULL ELSE asin({0}) END");

        Method("Atan2")
            .Doc("Вычисляет арктангенс y/x с учетом квадранта")
            .Arg("y", DataType.Number)
            .Arg("x", DataType.Number)
            .Returns(DataType.Number)
            .Template($"atan2({1}, {0})");

        Method("Acosh")
            .Doc("Вычисляет гиперболический арккосинус")
            .Arg("value", DataType.Number)
            .Returns(DataType.Number)
            .CustomNullPropagation(_ => true)
            .Template($"if({0} >= 1, acosh({0}), NULL)");

        Method("Atanh")
            .Doc("Вычисляет гиперболический арктангенс")
            .Arg("value", DataType.Number)
            .Returns(DataType.Number)
            .CustomNullPropagation(_ => true)
            .Template($"if(ABS({0}) < 1, atanh({0}), NULL)");

        Method("Rad")
            .Doc("Преобразует градусы в радианы")
            .Arg("degrees", DataType.Number)
            .Returns(DataType.Number)
            .Template($"{0} * PI() / 180");

        Method("Deg")
            .Doc("Преобразует радианы в градусы")
            .Arg("radians", DataType.Number)
            .Returns(DataType.Number)
            .Template($"{0} * 180 / PI()");
    }
}

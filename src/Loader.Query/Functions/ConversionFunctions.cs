using Loader.Query.Models;
using Loader.Query.Resolve;
using Loader.Query.Template;
using QueryTemplate = Loader.Query.Template.Template;

namespace Loader.Query.Functions;

public sealed class ConversionFunctions : FunctionDescriptor
{
    protected override void DefineFunctions()
    {
        foreach (var type in new[]
                 {
                     DataType.Number,
                     DataType.Integer,
                     DataType.Text,
                     DataType.Boolean,
                     DataType.DateTime
                 })
        {
            Conversion(type, type)
                .Doc("Не производит никаких действий")
                .Template($"{0}");
        }

        Conversion(DataType.Text, DataType.Integer)
            .Doc("Преобразует текст в целое число")
            .Template($"CAST({0} AS Int64)");

        Conversion(DataType.Boolean, DataType.Integer)
            .Doc("Преобразует логическое значение в целое число")
            .Template($"CASE WHEN {0} THEN 1 ELSE 0 END");

        Conversion(DataType.Number, DataType.Integer)
            .Doc("Преобразует число в целое число")
            .Template($"CAST({0} AS Int64)");

        Conversion(DataType.Null, DataType.Integer)
            .Template($"({0} + 0)");

        Conversion(DataType.Text, DataType.Number)
            .Doc("Преобразует текст в число с плавающей точкой")
            .Template($"toDecimal64({0}, 10)");

        Conversion(DataType.Boolean, DataType.Number)
            .Doc("Преобразует логическое значение в число")
            .Template($"CASE WHEN {0} THEN 1.0 ELSE 0.0 END");

        Conversion(DataType.Integer, DataType.Number)
            .Doc("Преобразует целое число в число с плавающей точкой")
            .Template($"toDecimal64({0}, 10)");

        Conversion(DataType.Null, DataType.Number)
            .Template($"({0} + 0.0)");

        Conversion(DataType.Text, DataType.Boolean)
            .Doc("Возвращает true если текст не пустой")
            .Template($"(LENGTH({0}) > 0)");

        Conversion(DataType.Number, DataType.Boolean)
            .Doc("Возвращает true если дробное число больше нуля")
            .Template($"({0} > 0.0)");

        Conversion(DataType.Integer, DataType.Boolean)
            .Doc("Возвращает true если целое число больше нуля")
            .Template($"({0} > 0)");

        Conversion(DataType.Null, DataType.Boolean)
            .Template($"({0} = 0)");

        Method("Text")
            .Doc("Преобразует целое число в текстовое представление")
            .Arg("input", DataType.Integer)
            .Returns(DataType.Text)
            .Template($"toString({0})");

        Method("Text")
            .Doc("Преобразует число в текстовое представление")
            .Arg("input", DataType.Number)
            .Returns(DataType.Text)
            .Template($"toString({0})");

        Method("Text")
            .Doc("Преобразует логическое значение в текст")
            .Arg("input", DataType.Boolean)
            .Returns(DataType.Text)
            .Template($"toString({0})");

        Method("Text")
            .Doc("Не производит никаких действий")
            .Arg("input", DataType.Text)
            .Returns(DataType.Text)
            .Template($"{0}");

        Method("Text")
            .Arg("input", DataType.Null)
            .Returns(DataType.Text)
            .Template("NULL");

        Method("Text")
            .Doc("Преобразует дату в текстовое представление в формате ISO")
            .Arg("input", DataType.DateTime)
            .Returns(DataType.Text)
            .Template($"formatDateTime({0}, '%Y-%m-%d %H:%i:%S')");

        Method("Text")
            .Doc("Преобразует значение неизвестного типа в текстовое представление")
            .Arg("input", DataType.Unknown)
            .Returns(DataType.Text)
            .Template($"toString({0})");

        Method("Type")
            .Doc("Возвращает тип значения в виде строки")
            .Arg("input", DataType.Unknown)
            .ReturnsNotNull(DataType.Text, ConstPropagation.AlwaysTrue)
            .Template(arguments => QueryTemplate.Text($"'{Display(arguments[0].Type)}'"));
    }

    private FunctionBuilder Conversion(DataType input, DataType output)
    {
        var name = output switch
        {
            DataType.Number => "Num",
            DataType.Integer => "Int",
            DataType.Text => "Text",
            DataType.DateTime => "Date",
            DataType.Boolean => "Bool",
            _ => throw new ArgumentOutOfRangeException(nameof(output), output, null)
        };

        return Method(name)
            .Arg("input", input)
            .Returns(output);
    }

    private static string Display(ExprType type)
    {
        return type.DataType switch
        {
            DataType.Null => "null",
            DataType.Number => type.CanBeNull ? "num" : "num!",
            DataType.Integer => type.CanBeNull ? "int" : "int!",
            DataType.Text => type.CanBeNull ? "text" : "text!",
            DataType.Boolean => type.CanBeNull ? "bool" : "bool!",
            DataType.DateTime => type.CanBeNull ? "date" : "date!",
            DataType.Unknown => type.CanBeNull ? "unk" : "unk!",
            _ => "unk"
        };
    }
}

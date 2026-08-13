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

        Method("Int")
            .Doc("Преобразует текст в целое число")
            .Arg("input", DataType.Text)
            .Returns(DataType.Integer)
            .CustomNullPropagation(static _ => true)
            .Template($"toInt64OrNull({0})");

        RequiredConversion(DataType.Boolean, DataType.Integer)
            .Doc("Преобразует логическое значение в целое число")
            .Template($"CAST({0} AS Int64)");
            
        NullableConversion(DataType.Boolean, DataType.Integer)
            .Doc("Преобразует логическое значение в целое число")
            .Template($"CAST({0} AS Nullable(Int64))");

        RequiredConversion(DataType.Number, DataType.Integer)
            .Doc("Преобразует число в целое число")
            .Template($"CAST({0} AS Int64)");
            
        NullableConversion(DataType.Number, DataType.Integer)
            .Doc("Преобразует число в целое число")
            .Template($"CAST({0} AS Nullable(Int64))");

        NullableConversion(DataType.Null, DataType.Integer)
            .Template($"CAST({0} AS Nullable(Int64))");

        Method("Num")
            .Doc("Преобразует текст в число с плавающей точкой")
            .Arg("input", DataType.Text)
            .Returns(DataType.Number)
            .CustomNullPropagation(static _ => true)
            .Template($"toDecimal64OrNull({0}, 10)");

        Method("Num")
            .Doc("Преобразует текст в число с указанным десятичным разделителем")
            .Arg("input", DataType.Text)
            .ConstArg("decimalSeparator", DataType.Text)
            .Returns(DataType.Number)
            .CustomNullPropagation(static _ => true)
            .Template($"if(isNull({0}) OR {1} = '', CAST(NULL AS Nullable(Decimal64(10))), toDecimal64OrNull(replaceAll(ifNull({0}, ''), {1}, '.'), 10))");

        RequiredConversion(DataType.Boolean, DataType.Number)
            .Doc("Преобразует логическое значение в число")
            .Template($"CAST({0} AS Decimal64(10))");
            
        NullableConversion(DataType.Boolean, DataType.Number)
            .Doc("Преобразует логическое значение в число")
            .Template($"CAST({0} AS Nullable(Decimal64(10)))");

        RequiredConversion(DataType.Integer, DataType.Number)
            .Doc("Преобразует целое число в число с плавающей точкой")
            .Template($"CAST({0} AS Decimal64(10))");
            
        NullableConversion(DataType.Integer, DataType.Number)
            .Doc("Преобразует целое число в число с плавающей точкой")
            .Template($"CAST({0} AS Nullable(Decimal64(10)))");

        NullableConversion(DataType.Null, DataType.Number)
            .Template($"CAST({0} AS Nullable(Decimal64(10)))");

        RequiredConversion(DataType.Text, DataType.Boolean)
            .Doc("Возвращает true если текст не пустой")
            .Template($"CAST((LENGTH({0}) > 0) AS Bool)");
            
        NullableConversion(DataType.Text, DataType.Boolean)
            .Doc("Возвращает true если текст не пустой")
            .Template($"CAST((LENGTH({0}) > 0) AS Nullable(Bool))");

        RequiredConversion(DataType.Number, DataType.Boolean)
            .Doc("Возвращает true если дробное число больше нуля")
            .Template($"CAST(({0} > 0.0) AS Bool)");
            
        NullableConversion(DataType.Number, DataType.Boolean)
            .Doc("Возвращает true если дробное число больше нуля")
            .Template($"CAST(({0} > 0.0) AS Nullable(Bool))");

        RequiredConversion(DataType.Integer, DataType.Boolean)
            .Doc("Возвращает true если целое число больше нуля")
            .Template($"CAST(({0} > 0) AS Bool)");
            
        NullableConversion(DataType.Integer, DataType.Boolean)
            .Doc("Возвращает true если целое число больше нуля")
            .Template($"CAST(({0} > 0) AS Nullable(Bool))");

        NullableConversion(DataType.Null, DataType.Boolean)
            .Template($"CAST({0} AS Nullable(Bool))");

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
            .Doc("Преобразует дату в текст по Joda format")
            .Arg("input", DataType.DateTime)
            .ConstArg("format", DataType.Text)
            .Returns(DataType.Text)
            .Template($"formatDateTimeInJodaSyntax({0}, {1})");

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
        return NullableConversion(input, output);
    }

    private FunctionBuilder RequiredConversion(DataType input, DataType output)
    {
        var builder = CreateConversionBuilder(output)
            .ReqArg("input", input)
            .ReturnsNotNull(output);
        return builder;
    }

    private FunctionBuilder NullableConversion(DataType input, DataType output)
    {
        var builder = CreateConversionBuilder(output)
            .Arg("input", input)
            .Returns(output);
        return builder;
    }

    private FunctionBuilder CreateConversionBuilder(DataType output)
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

        return Method(name);
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
            DataType.Time => type.CanBeNull ? "time" : "time!",
            DataType.Unknown => type.CanBeNull ? "unk" : "unk!",
            _ => "unk"
        };
    }
}

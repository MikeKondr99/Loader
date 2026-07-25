using Loader.Query.Models;

namespace Loader.Query.Functions;

public sealed class JsonFunctions : FunctionDescriptor
{
    protected override void DefineFunctions()
    {
        Method("JsonGet")
            .Doc("Возвращает raw JSON fragment по ClickHouse JSONPath или NULL, если путь не найден")
            .Arg("input", DataType.Text)
            .ConstArg("path", DataType.Text)
            .Returns(DataType.Text)
            .CustomNullPropagation(static _ => true)
            .Template($"nullIf(JSONExtractRaw(JSON_QUERY({0}, {1}), 1), '')");

        Method("JsonHas")
            .Doc("Проверяет, существует ли значение по ClickHouse JSONPath")
            .Arg("input", DataType.Text)
            .ConstArg("path", DataType.Text)
            .Returns(DataType.Boolean)
            .Template($"JSON_EXISTS({0}, {1})");

        Method("JsonType")
            .Doc("Возвращает тип root JSON значения")
            .Arg("input", DataType.Text)
            .Returns(DataType.Text)
            .Template($"JSONType({0})");

        Method("JsonType")
            .Doc("Возвращает тип JSON значения по ClickHouse JSONPath или NULL, если путь не найден")
            .Arg("input", DataType.Text)
            .ConstArg("path", DataType.Text)
            .Returns(DataType.Text)
            .CustomNullPropagation(static _ => true)
            .Template($"CASE WHEN JSON_EXISTS({0}, {1}) THEN JSONType(JSONExtractRaw(JSON_QUERY({0}, {1}), 1)) ELSE NULL END");

        Method("JsonLength")
            .Doc("Возвращает длину root JSON array/object")
            .Arg("input", DataType.Text)
            .Returns(DataType.Integer)
            .Template($"JSONLength({0})");

        Method("JsonLength")
            .Doc("Возвращает длину JSON array/object по ClickHouse JSONPath или NULL, если путь не найден")
            .Arg("input", DataType.Text)
            .ConstArg("path", DataType.Text)
            .Returns(DataType.Integer)
            .CustomNullPropagation(static _ => true)
            .Template($"CASE WHEN JSON_EXISTS({0}, {1}) THEN JSONLength(JSONExtractRaw(JSON_QUERY({0}, {1}), 1)) ELSE NULL END");

        Method("JsonGetText")
            .Doc("Возвращает JSON scalar как текст")
            .Arg("input", DataType.Text)
            .Returns(DataType.Text)
            .CustomNullPropagation(static _ => true)
            .Template($"JSON_VALUE({0}, '$')");

        Method("JsonGetText")
            .Doc("Возвращает JSON scalar как текст")
            .Arg("input", DataType.Text)
            .ConstArg("path", DataType.Text)
            .Returns(DataType.Text)
            .CustomNullPropagation(static _ => true)
            .Template($"CASE WHEN JSON_EXISTS({0}, {1}) THEN JSON_VALUE({0}, {1}) ELSE NULL END");

        Method("JsonGetInt")
            .Doc("Возвращает JSON scalar как целое число")
            .Arg("input", DataType.Text)
            .Returns(DataType.Integer)
            .CustomNullPropagation(static _ => true)
            .Template($"toInt64OrNull(JSON_VALUE({0}, '$'))");

        Method("JsonGetInt")
            .Doc("Возвращает JSON scalar как целое число")
            .Arg("input", DataType.Text)
            .ConstArg("path", DataType.Text)
            .Returns(DataType.Integer)
            .CustomNullPropagation(static _ => true)
            .Template($"toInt64OrNull(CASE WHEN JSON_EXISTS({0}, {1}) THEN JSON_VALUE({0}, {1}) ELSE NULL END)");

        Method("JsonGetNum")
            .Doc("Возвращает JSON scalar как дробное число")
            .Arg("input", DataType.Text)
            .Returns(DataType.Number)
            .CustomNullPropagation(static _ => true)
            .Template($"toDecimal64OrNull(JSON_VALUE({0}, '$'), 10)");

        Method("JsonGetNum")
            .Doc("Возвращает JSON scalar как дробное число")
            .Arg("input", DataType.Text)
            .ConstArg("path", DataType.Text)
            .Returns(DataType.Number)
            .CustomNullPropagation(static _ => true)
            .Template($"toDecimal64OrNull(CASE WHEN JSON_EXISTS({0}, {1}) THEN JSON_VALUE({0}, {1}) ELSE NULL END, 10)");

        Method("JsonGetBool")
            .Doc("Возвращает JSON scalar как логическое значение")
            .Arg("input", DataType.Text)
            .Returns(DataType.Boolean)
            .CustomNullPropagation(static _ => true)
            .Template($"CASE JSON_VALUE({0}, '$') WHEN 'true' THEN true WHEN 'false' THEN false WHEN '1' THEN true WHEN '0' THEN false ELSE NULL END");

        Method("JsonGetBool")
            .Doc("Возвращает JSON scalar как логическое значение")
            .Arg("input", DataType.Text)
            .ConstArg("path", DataType.Text)
            .Returns(DataType.Boolean)
            .CustomNullPropagation(static _ => true)
            .Template($"CASE CASE WHEN JSON_EXISTS({0}, {1}) THEN JSON_VALUE({0}, {1}) ELSE NULL END WHEN 'true' THEN true WHEN 'false' THEN false WHEN '1' THEN true WHEN '0' THEN false ELSE NULL END");
    }
}

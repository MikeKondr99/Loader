using Loader.Query.Models;

namespace Loader.Query.Functions;

public sealed class ConditionalFunctions : FunctionDescriptor
{
    protected override void DefineFunctions()
    {
        foreach (var type in new[]
                 {
                     DataType.Number,
                     DataType.Text,
                     DataType.Integer,
                     DataType.DateTime
                 })
        {
            Function("If")
                .Doc("Условное выражение: возвращает then-значение если condition=true, иначе else-значение")
                .Arg("condition", DataType.Boolean, propagateNull: false)
                .Arg("then", type)
                .Arg("else", type)
                .Returns(type)
                .Template($"CASE WHEN {0} THEN {1} ELSE {2} END");

            Function("Case")
                .Doc("Возвращает значение, если условие истинно, иначе NULL")
                .Arg("condition", DataType.Boolean)
                .ReqArg("then", type)
                .Returns(type)
                .CustomNullPropagation(static _ => true)
                .Template($"CASE WHEN {0} THEN {1} ELSE NULL END");

            Method("Case")
                .Doc("Возвращает input, если он не NULL, иначе возвращает alt при истинном condition")
                .Arg("input", type)
                .Arg("condition", DataType.Boolean)
                .ReqArg("alt", type)
                .Returns(type)
                .CustomNullPropagation(static _ => true)
                .Template($"COALESCE({0}, CASE WHEN {1} THEN {2} ELSE NULL END)");

            Method("Alt")
                .Doc("Возвращает первое значение, если оно не NULL, иначе возвращает альтернативное значение")
                .Arg("input", type)
                .Arg("alt", type)
                .Returns(type)
                .CustomNullPropagation(static nulls => nulls.All(static value => value))
                .Template($"COALESCE({0}, {1})");
        }

        foreach (var type in new[]
                 {
                     DataType.Number,
                     DataType.Text,
                     DataType.Integer,
                     DataType.DateTime,
                     DataType.Boolean,
                     DataType.Unknown
                 })
        {
            Method("IsNull")
                .Doc("Проверяет, является ли значение NULL")
                .Arg("value", type)
                .ReturnsNotNull(DataType.Boolean)
                .Template($"({0} IS NULL)");

            Method("NotNull")
                .Doc("Проверяет, что значение не NULL")
                .Arg("value", type)
                .ReturnsNotNull(DataType.Boolean)
                .Template($"({0} IS NOT NULL)");
        }
    }
}

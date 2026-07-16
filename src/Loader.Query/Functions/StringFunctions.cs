using Loader.Query.Models;

namespace Loader.Query.Functions;

public sealed class StringFunctions : FunctionDescriptor
{
    protected override void DefineFunctions()
    {
        Method("Substring")
            .Doc("Возвращает подстроку начиная с указанной позиции")
            .Arg("input", DataType.Text)
            .Arg("start", DataType.Integer)
            .Returns(DataType.Text)
            .Template($"SUBSTRING({0}, {1}, LENGTH({0}) - ({1} - 1))");

        Method("Substring")
            .Doc("Возвращает подстроку указанной длины")
            .Arg("input", DataType.Text)
            .Arg("start", DataType.Integer)
            .Arg("count", DataType.Integer)
            .Returns(DataType.Text)
            .Template($"SUBSTRING({0}, {1}, {2})");

        Method("PadLeft")
            .Doc("Дополняет строку слева пробелами до указанной длины")
            .Arg("input", DataType.Text)
            .Arg("count", DataType.Integer)
            .Returns(DataType.Text)
            .Template($"if({1} >= 0, if(lengthUTF8({0}) >= {1}, substringUTF8({0}, 1, {1}), substringUTF8(concat(repeat(' ', {1}), {0}), -{1}, {1})), '')");

        Method("PadRight")
            .Doc("Дополняет строку справа пробелами до указанной длины")
            .Arg("input", DataType.Text)
            .Arg("count", DataType.Integer)
            .Returns(DataType.Text)
            .Template($"if({1} >= 0, substringUTF8(concat({0}, repeat(' ', {1})), 1, {1}), '')");

        Method("PadLeft")
            .Doc("Дополняет строку слева указанным символом до указанной длины")
            .Arg("input", DataType.Text)
            .Arg("count", DataType.Integer)
            .Arg("symbol", DataType.Text)
            .Returns(DataType.Text)
            .Template($"if({1} >= 0, if(lengthUTF8({0}) >= {1}, substringUTF8({0}, 1, {1}), substringUTF8(concat(repeat({2}, {1}), {0}), -{1}, {1})), '')");

        Method("PadRight")
            .Doc("Дополняет строку справа указанным символом до указанной длины")
            .Arg("input", DataType.Text)
            .Arg("count", DataType.Integer)
            .Arg("symbol", DataType.Text)
            .Returns(DataType.Text)
            .Template($"if({1} >= 0, substringUTF8(concat({0}, repeat({2}, {1})), 1, {1}), '')");

        Method("Lower")
            .Doc("Преобразует текст в нижний регистр")
            .Arg("input", DataType.Text)
            .Returns(DataType.Text)
            .Template($"LOWER({0})");

        Method("Upper")
            .Doc("Преобразует текст в верхний регистр")
            .Arg("input", DataType.Text)
            .Returns(DataType.Text)
            .Template($"UPPER({0})");

        Method("Trim")
            .Doc("Удаляет пробелы в начале и конце строки")
            .Arg("input", DataType.Text)
            .Returns(DataType.Text)
            .Template($"TRIM({0})");

        Method("TrimLeft")
            .Doc("Удаляет пробелы в начале строки")
            .Arg("input", DataType.Text)
            .Returns(DataType.Text)
            .Template($"LTRIM({0})");

        Method("TrimRight")
            .Doc("Удаляет пробелы в конце строки")
            .Arg("input", DataType.Text)
            .Returns(DataType.Text)
            .Template($"RTRIM({0})");

        Method("Reverse")
            .Doc("Разворачивает строку")
            .Arg("input", DataType.Text)
            .Returns(DataType.Text)
            .Template($"reverseUTF8({0})");

        Binary("+", DataType.Text, DataType.Text)
            .Doc("Склеивает две строки")
            .Returns(DataType.Text)
            .Template($"CONCAT({0}, {1})");

        Method("EmptyIsNull")
            .Doc("Возвращает NULL вместо пустой строки")
            .Arg("input", DataType.Text)
            .Returns(DataType.Text)
            .CustomNullPropagation(_ => true)
            .Template($"nullIf({0}, '')");

        Method("Replace")
            .Doc("Заменяет все вхождения одной строки на другую")
            .Arg("input", DataType.Text)
            .Arg("from", DataType.Text)
            .Arg("to", DataType.Text)
            .Returns(DataType.Text)
            .Template($"REPLACE({0}, {1}, {2})");

        Method("Repeat")
            .Doc("Повторяет строку указанное количество раз")
            .Arg("input", DataType.Text)
            .Arg("count", DataType.Integer)
            .Returns(DataType.Text)
            .Template($"repeat({0}, greatest({1}, 0))");

        Method("Index")
            .Doc("Возвращает позицию первого вхождения подстроки")
            .Arg("input", DataType.Text)
            .Arg("substring", DataType.Text)
            .Returns(DataType.Integer)
            .CustomNullPropagation(_ => true)
            .Template($"nullIf(positionUTF8({0}, {1}), 0)");

        Method("Contains")
            .Doc("Проверяет, содержит ли строка подстроку")
            .Arg("input", DataType.Text)
            .Arg("substring", DataType.Text)
            .Returns(DataType.Boolean)
            .CustomNullPropagation(_ => true)
            .Template($"(positionUTF8({0}, {1}) > 0)");

        Method("StartsWith")
            .Doc("Проверяет, начинается ли строка с подстроки")
            .Arg("input", DataType.Text)
            .Arg("substring", DataType.Text)
            .Returns(DataType.Boolean)
            .CustomNullPropagation(_ => true)
            .Template($"(substringUTF8({0}, 1, lengthUTF8({1})) = {1})");

        Method("EndsWith")
            .Doc("Проверяет, заканчивается ли строка подстрокой")
            .Arg("input", DataType.Text)
            .Arg("substring", DataType.Text)
            .Returns(DataType.Boolean)
            .CustomNullPropagation(_ => true)
            .Template($"(substringUTF8({0}, -lengthUTF8({1})) = {1})");

        Method("LastIndex")
            .Doc("Возвращает позицию последнего вхождения подстроки")
            .Arg("input", DataType.Text)
            .Arg("substring", DataType.Text)
            .Returns(DataType.Integer)
            .CustomNullPropagation(_ => true)
            .Template($"(lengthUTF8({0}) - lengthUTF8({1}) + 2 - nullIf(positionUTF8(reverseUTF8({0}), reverseUTF8({1})), 0))");

        Method("Len")
            .Doc("Возвращает длину строки")
            .Arg("input", DataType.Text)
            .Returns(DataType.Integer)
            .Template($"lengthUTF8({0})");

        Method("Chr")
            .Doc("Возвращает символ по Unicode-коду")
            .Arg("code", DataType.Integer)
            .Returns(DataType.Text)
            .CustomNullPropagation(_ => true)
            .Template($"CASE WHEN {0} = 0 THEN char(0) WHEN {0} BETWEEN 1 AND 1114111 THEN decodeXMLComponent(concat('&#', toString({0}), ';')) ELSE NULL END");
    }
}

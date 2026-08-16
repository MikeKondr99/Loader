using System.Text;
using Loader.Core.Models;
using Loader.Core.Writers.ClickHouse;
using Loader.Lang;

namespace Loader.Script.Execution;

/// <summary>
/// Собирает ClickHouse SQL для Join/LeftJoin/RightJoin/FullJoin.
/// Сборщик намеренно строит соединение через вложенные SELECT-фрагменты, а не напрямую через имена таблиц:
/// сейчас фрагмент читает финальную таблицу, но позже его можно заменить на SQL-фрагмент временной таблицы
/// без изменения логики ON, внутренних псевдонимов join_columnN и разрешения конфликтов пользовательских имен.
/// </summary>
internal static class JoinSqlBuilder
{
    public static JoinSql? Build(
        LoadedTable left,
        string leftKey,
        LoadedTable right,
        string rightKey,
        JoinKind kind,
        LangSpan span,
        List<LangError> errors)
    {
        var leftKeyIndex = FindFieldIndex(left, leftKey);
        var rightKeyIndex = FindFieldIndex(right, rightKey);
        if (leftKeyIndex < 0 || rightKeyIndex < 0)
        {
            throw new InvalidOperationException("Join key fields must be validated before SQL build.");
        }

        var fields = BuildOutputFields(left, right, kind, span, errors);
        if (errors.Count > 0)
        {
            return null;
        }

        return new JoinSql(
            BuildSql(left, leftKeyIndex, right, rightKeyIndex, kind, fields.Count),
            fields);
    }

    public static int FindFieldIndex(LoadedTable table, string name)
    {
        return table.Fields.FindIndex(field => string.Equals(field.Name, name, StringComparison.Ordinal));
    }

    private static List<LoadedTableField> BuildOutputFields(
        LoadedTable left,
        LoadedTable right,
        JoinKind kind,
        LangSpan span,
        List<LangError> errors)
    {
        var rawNameCounts = left.Fields
            .Concat(right.Fields)
            .GroupBy(static field => field.Name, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);

        var result = new List<LoadedTableField>(left.Fields.Count + right.Fields.Count);
        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        AppendTableFields(left, JoinSide.Left, kind, rawNameCounts, result, usedNames, span, errors);
        AppendTableFields(right, JoinSide.Right, kind, rawNameCounts, result, usedNames, span, errors);
        return result;
    }

    private static void AppendTableFields(
        LoadedTable table,
        JoinSide side,
        JoinKind kind,
        IReadOnlyDictionary<string, int> rawNameCounts,
        List<LoadedTableField> result,
        HashSet<string> usedNames,
        LangSpan span,
        List<LangError> errors)
    {
        var tableAlias = table.Alias ?? table.Name.Table;
        foreach (var field in table.Fields)
        {
            var outputName = rawNameCounts[field.Name] > 1
                ? $"{tableAlias}.{field.Name}"
                : field.Name;

            if (!usedNames.Add(outputName))
            {
                errors.Add(new LangError
                {
                    Message = $"Join не может разрешить конфликт имени поля '{outputName}'.",
                    Span = span
                });
                continue;
            }

            result.Add(field with
            {
                Name = outputName,
                CanBeNull = ShouldBeNullable(field, side, kind)
            });
        }
    }

    private static bool ShouldBeNullable(LoadedTableField field, JoinSide side, JoinKind kind)
    {
        return field.CanBeNull ||
               kind == JoinKind.Full ||
               kind == JoinKind.Left && side == JoinSide.Right ||
               kind == JoinKind.Right && side == JoinSide.Left;
    }

    private static string BuildSql(
        LoadedTable left,
        int leftKeyIndex,
        LoadedTable right,
        int rightKeyIndex,
        JoinKind kind,
        int outputFieldCount)
    {
        var builder = new StringBuilder();
        builder.AppendLine("SELECT");
        for (var ordinal = 0; ordinal < outputFieldCount; ordinal++)
        {
            if (ordinal > 0)
            {
                builder.AppendLine(",");
            }

            var sourceAlias = ordinal < left.Fields.Count ? "l" : "r";
            var sourceOrdinal = ordinal < left.Fields.Count
                ? ordinal
                : ordinal - left.Fields.Count;
            builder
                .Append("    ")
                .Append(sourceAlias)
                .Append('.')
                .Append(Identifier($"column{sourceOrdinal + 1}"))
                .Append(" AS ")
                .Append(Identifier($"join_column{ordinal + 1}"));
        }

        builder
            .AppendLine()
            .AppendLine("FROM")
            .AppendLine("(");
        AppendIndented(builder, BuildTableReadSql(left), 4);
        builder
            .AppendLine(") AS l")
            .Append(JoinKeyword(kind))
            .AppendLine()
            .AppendLine("(");
        AppendIndented(builder, BuildTableReadSql(right), 4);
        builder
            .AppendLine(") AS r")
            .Append("ON l.")
            .Append(Identifier($"column{leftKeyIndex + 1}"))
            .Append(" = r.")
            .Append(Identifier($"column{rightKeyIndex + 1}"))
            .AppendLine()
            .Append("SETTINGS join_use_nulls = 1");

        return builder.ToString();
    }

    private static string BuildTableReadSql(LoadedTable table)
    {
        var builder = new StringBuilder();
        builder.AppendLine("SELECT");
        for (var ordinal = 0; ordinal < table.Fields.Count; ordinal++)
        {
            if (ordinal > 0)
            {
                builder.AppendLine(",");
            }

            builder
                .Append("    ")
                .Append(Identifier($"column{ordinal + 1}"));
        }

        builder
            .AppendLine()
            .Append("FROM ")
            .Append(table.Name.ToSql());
        return builder.ToString();
    }

    private static string JoinKeyword(JoinKind kind)
    {
        return kind switch
        {
            JoinKind.Inner => "INNER JOIN",
            JoinKind.Left => "LEFT JOIN",
            JoinKind.Right => "RIGHT JOIN",
            JoinKind.Full => "FULL OUTER JOIN",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    private static void AppendIndented(StringBuilder builder, string text, int spaces)
    {
        var indent = new string(' ', spaces);
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
        {
            builder
                .Append(indent)
                .AppendLine(line);
        }
    }

    private static string Identifier(string value)
    {
        var builder = new StringBuilder();
        builder.Append('`');
        foreach (var character in value)
        {
            if (character == '`')
            {
                builder.Append("``");
                continue;
            }

            builder.Append(character);
        }

        builder.Append('`');
        return builder.ToString();
    }

    private enum JoinSide
    {
        Left,
        Right
    }
}

internal sealed record JoinSql(string Sql, IReadOnlyList<LoadedTableField> Fields);

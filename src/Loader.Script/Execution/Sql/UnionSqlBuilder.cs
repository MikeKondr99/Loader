using System.Text;
using Loader.Core.Models;
using Loader.Core.Writers.ClickHouse;

namespace Loader.Script.Execution;

/// <summary>
/// Собирает ClickHouse SQL для provider-а Union.
/// Важные инварианты:
/// 1. ClickHouse UNION ALL совмещает колонки по порядку, не по имени.
/// 2. Финальные таблицы Loader хранят физические имена column1/column2, а пользовательские alias-ы живут в LoadedTable.
/// 3. Поэтому builder сначала строит общий список логических полей, потом для каждой таблицы выбирает значения
///    строго в этом порядке и выдает безопасные внутренние alias-ы union_columnN.
/// 4. Сейчас всегда читаем final table name. Позже эту точку можно заменить на SQL-фрагмент таблицы
///    без изменения алгоритма выравнивания полей.
/// </summary>
internal static class UnionSqlBuilder
{
    public static UnionSql Build(IReadOnlyList<LoadedTable> tables)
    {
        if (tables.Count == 0)
        {
            throw new ArgumentException("Union requires at least one loaded table.", nameof(tables));
        }

        var fields = BuildUnionFields(tables);
        var sql = BuildUnionSql(tables, fields);
        return new UnionSql(sql, fields);
    }

    private static List<LoadedTableField> BuildUnionFields(IReadOnlyList<LoadedTable> tables)
    {
        var fields = new List<LoadedTableField>();
        var indexes = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var table in tables)
        {
            foreach (var field in table.Fields)
            {
                if (!indexes.TryGetValue(field.Name, out var index))
                {
                    indexes.Add(field.Name, fields.Count);
                    fields.Add(field with { CanBeNull = true });
                    continue;
                }

                fields[index] = MergeField(fields[index], field);
            }
        }

        return fields;
    }

    private static LoadedTableField MergeField(LoadedTableField current, LoadedTableField next)
    {
        return current with
        {
            DataType = MergeDataType(current.DataType, next.DataType),
            CanBeNull = true
        };
    }

    private static DataType MergeDataType(DataType left, DataType right)
    {
        if (left == right)
        {
            return left;
        }

        if (left == DataType.Text || right == DataType.Text)
        {
            return DataType.Text;
        }

        if (left == DataType.Number || right == DataType.Number)
        {
            return DataType.Number;
        }

        if (left == DataType.Integer && right == DataType.Boolean ||
            left == DataType.Boolean && right == DataType.Integer)
        {
            return DataType.Integer;
        }

        return DataType.Text;
    }

    private static string BuildUnionSql(
        IReadOnlyList<LoadedTable> tables,
        IReadOnlyList<LoadedTableField> fields)
    {
        var builder = new StringBuilder();
        for (var tableIndex = 0; tableIndex < tables.Count; tableIndex++)
        {
            if (tableIndex > 0)
            {
                builder.AppendLine();
                builder.AppendLine("UNION ALL");
            }

            AppendTableSelect(builder, tables[tableIndex], fields);
        }

        return builder.ToString();
    }

    private static void AppendTableSelect(
        StringBuilder builder,
        LoadedTable table,
        IReadOnlyList<LoadedTableField> unionFields)
    {
        var tableFields = table.Fields
            .Select((field, ordinal) => (field, ordinal))
            .ToDictionary(static item => item.field.Name, static item => item, StringComparer.Ordinal);

        builder.AppendLine("SELECT");
        for (var index = 0; index < unionFields.Count; index++)
        {
            if (index > 0)
            {
                builder.AppendLine(",");
            }

            var unionField = unionFields[index];
            builder.Append("    ");
            if (tableFields.TryGetValue(unionField.Name, out var tableField))
            {
                builder.Append(CastExpression(
                    Identifier($"column{tableField.ordinal + 1}"),
                    tableField.field.DataType,
                    unionField.DataType));
            }
            else
            {
                builder.Append(NullExpression(unionField.DataType));
            }

            builder
                .Append(" AS ")
                .Append(UnionColumnName(index));
        }

        builder.AppendLine();
        builder
            .Append("FROM ")
            .Append(table.Name.ToSql());
    }

    private static string CastExpression(
        string expression,
        DataType sourceType,
        DataType targetType)
    {
        var targetClickHouseType = NullableClickHouseType(targetType);
        if (sourceType == targetType)
        {
            return $"CAST({expression} AS {targetClickHouseType})";
        }

        return targetType switch
        {
            DataType.Text => $"CAST(toString({expression}) AS {targetClickHouseType})",
            DataType.Number => $"CAST(toDecimal64OrNull(toString({expression}), 10) AS {targetClickHouseType})",
            DataType.Integer when sourceType == DataType.Boolean =>
                $"CAST(CASE WHEN {expression} THEN 1 ELSE 0 END AS {targetClickHouseType})",
            DataType.Integer => $"CAST(toInt64OrNull(toString({expression})) AS {targetClickHouseType})",
            DataType.Boolean => $"CAST(toBool({expression}) AS {targetClickHouseType})",
            DataType.Date => $"CAST(toDateOrNull(toString({expression})) AS {targetClickHouseType})",
            DataType.DateTime => $"CAST(parseDateTime64BestEffortOrNull(toString({expression}), 3) AS {targetClickHouseType})",
            DataType.Time => $"CAST({expression} AS {targetClickHouseType})",
            _ => $"CAST(toString({expression}) AS Nullable(String))"
        };
    }

    private static string NullExpression(DataType dataType)
    {
        return $"CAST(NULL AS {NullableClickHouseType(dataType)})";
    }

    private static string NullableClickHouseType(DataType dataType)
    {
        return dataType switch
        {
            DataType.Text => "Nullable(String)",
            DataType.Integer => "Nullable(Int64)",
            DataType.Number => "Nullable(Decimal(38, 10))",
            DataType.Boolean => "Nullable(Bool)",
            DataType.Date => "Nullable(Date)",
            DataType.DateTime => "Nullable(DateTime64(3))",
            DataType.Time => "Nullable(DateTime)",
            _ => "Nullable(String)"
        };
    }

    private static string UnionColumnName(int index)
    {
        return Identifier($"union_column{index + 1}");
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
}

/// <summary>
/// SQL UNION ALL вместе с логической schema результата.
/// Поля нужны resolver-у, чтобы вернуть пользователю исходные aliases, а не внутренние union_columnN.
/// </summary>
internal sealed record UnionSql(string Sql, IReadOnlyList<LoadedTableField> Fields);

using System.Globalization;
using System.Text;
using Loader.Query.Models;
using QueryTemplate = Loader.Query.Template.Template;

namespace Loader.Query.Tests.Infrastructure;

internal static class InlineQueryArrange
{
    public static QuerySource Source(
        IReadOnlyList<InlineField> fields,
        IReadOnlyList<IReadOnlyList<string>> rows,
        string alias = "stage")
    {
        return new QuerySource
        {
            Sql = BuildInlineSql(fields, rows),
            Alias = alias,
            Fields = fields.Select(field => new Field
            {
                Alias = field.Alias,
                Template = QueryTemplate.Text($"{alias}.{QuoteIdentifier(field.Alias)}"),
                Type = new FieldType
                {
                    DataType = field.DataType,
                    CanBeNull = field.CanBeNull
                }
            }).ToArray()
        };
    }

    public static QuerySource SingleColumnSource(
        string fieldAlias,
        DataType dataType,
        IReadOnlyList<string> values,
        bool canBeNull = true,
        string alias = "stage")
    {
        var fields = new[]
        {
            new InlineField(fieldAlias, dataType, canBeNull)
        };
        var rows = values.Select(static value => (IReadOnlyList<string>)[value]).ToArray();
        return Source(fields, rows, alias);
    }

    private static string BuildInlineSql(
        IReadOnlyList<InlineField> fields,
        IReadOnlyList<IReadOnlyList<string>> rows)
    {
        if (fields.Count == 0)
        {
            throw new ArgumentException("Inline source must contain at least one field.", nameof(fields));
        }

        if (rows.Count == 0)
        {
            throw new ArgumentException("Inline source must contain at least one row.", nameof(rows));
        }

        var builder = new StringBuilder();
        builder.Append('(');
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            if (rowIndex > 0)
            {
                builder.Append(" UNION ALL ");
            }

            var row = rows[rowIndex];
            if (row.Count != fields.Count)
            {
                throw new ArgumentException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Inline row {rowIndex} contains {row.Count} values, expected {fields.Count}."),
                    nameof(rows));
            }

            builder.Append("SELECT ");
            for (var fieldIndex = 0; fieldIndex < fields.Count; fieldIndex++)
            {
                if (fieldIndex > 0)
                {
                    builder.Append(", ");
                }

                builder
                    .Append(row[fieldIndex])
                    .Append(" AS ")
                    .Append(QuoteIdentifier(fields[fieldIndex].Alias));
            }
        }

        builder.Append(')');
        return builder.ToString();
    }

    private static string QuoteIdentifier(string value)
    {
        var builder = new StringBuilder(value.Length + 2);
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

internal sealed record InlineField(string Alias, DataType DataType, bool CanBeNull = false);

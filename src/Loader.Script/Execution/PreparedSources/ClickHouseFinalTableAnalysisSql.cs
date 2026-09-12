using System.Text;
using Loader.Core.Models;

namespace Loader.Script.Execution;

internal static class ClickHouseFinalTableAnalysisSql
{
    public static string Build(
        string querySql,
        DataSchema schema,
        IReadOnlyList<string> sourceColumnNames)
    {
        if (schema.Fields.Count != sourceColumnNames.Count)
        {
            throw new ArgumentException(
                $"Source column names count {sourceColumnNames.Count} does not match schema field count {schema.Fields.Count}.",
                nameof(sourceColumnNames));
        }

        var builder = new StringBuilder();
        builder
            .AppendLine("SELECT")
            .Append("    count() AS `row_count`");

        for (var ordinal = 0; ordinal < schema.Fields.Count; ordinal++)
        {
            var field = schema.Fields[ordinal];
            var column = Identifier(sourceColumnNames[ordinal]);
            var prefix = $"c{ordinal}";

            builder
                .AppendLine(",")
                .Append("    count(")
                .Append(column)
                .Append(") AS ")
                .Append(Identifier($"{prefix}_non_null"))
                .AppendLine(",")
                .Append("    uniqCombined64(")
                .Append(column)
                .Append(") AS ")
                .Append(Identifier($"{prefix}_distinct"));

            if (field.DataType == DataType.Text)
            {
                builder
                    .AppendLine(",")
                    .Append("    NULL AS ")
                    .Append(Identifier($"{prefix}_min"))
                    .AppendLine(",")
                    .Append("    NULL AS ")
                    .Append(Identifier($"{prefix}_max"));
                continue;
            }

            builder
                .AppendLine(",")
                .Append("    min(")
                .Append(column)
                .Append(") AS ")
                .Append(Identifier($"{prefix}_min"))
                .AppendLine(",")
                .Append("    max(")
                .Append(column)
                .Append(") AS ")
                .Append(Identifier($"{prefix}_max"));
        }

        builder
            .AppendLine()
            .Append("FROM (")
            .Append(querySql)
            .Append(')');

        return builder.ToString();
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

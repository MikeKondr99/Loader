using System.Text;
using Loader.Core.Models;
using Loader.Core.Writers.ClickHouse;

namespace Loader.Script.Execution;

internal static class ClickHouseFinalTableSql
{
    public static string SchemaProbe(string querySql)
    {
        return $"SELECT * FROM ({querySql}) LIMIT 0";
    }

    public static string InsertSelect(
        ClickHouseTableName finalTable,
        DataSchema schema,
        string querySql)
    {
        var builder = new StringBuilder();
        builder
            .Append("INSERT INTO ")
            .Append(finalTable.ToSql())
            .Append(" (");

        for (var i = 0; i < schema.Fields.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            WriteIdentifier(builder, schema.Fields[i].Name);
        }

        builder
            .AppendLine(")")
            .Append("SELECT * FROM (")
            .Append(querySql)
            .Append(')');

        return builder.ToString();
    }

    public static string CountRows(ClickHouseTableName table)
    {
        return $"SELECT count() FROM {table.ToSql()}";
    }

    private static void WriteIdentifier(StringBuilder builder, string value)
    {
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
    }
}

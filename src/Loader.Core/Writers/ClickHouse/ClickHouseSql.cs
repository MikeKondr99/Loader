using System.Text;
using Loader.Core.Decorators;
using Loader.Core.Models;

namespace Loader.Core.Writers.ClickHouse;

/// <summary>
/// Единая точка сборки ClickHouse SQL.
/// Все методы пишут в переданный StringBuilder, чтобы сложный SQL собирался одним буфером без
/// промежуточных строк и без скрытой политики парсинга имен.
/// </summary>
internal static class ClickHouseSql
{
    public static string CreateTable(
        DataSchema schema,
        DataMetaContainer? meta,
        ClickHouseWriteOptions options,
        ClickHouseColumnTypeResolver typeResolver)
    {
        var builder = new StringBuilder();
        WriteCreateTable(builder, schema, meta, options, typeResolver);
        return builder.ToString();
    }

    public static void WriteCreateTable(
        StringBuilder builder,
        DataSchema schema,
        DataMetaContainer? meta,
        ClickHouseWriteOptions options,
        ClickHouseColumnTypeResolver typeResolver)
    {
        builder.Append("CREATE TABLE");
        if (options.IfNotExists)
        {
            builder.Append(" IF NOT EXISTS");
        }

        builder.Append(' ');
        WriteTableNameSql(builder, options.TableName);
        builder
            .AppendLine()
            .AppendLine("(");

        for (var i = 0; i < schema.Fields.Count; i++)
        {
            var field = schema.Fields[i];
            var columnMeta = FindMeta(field, meta);
            var type = typeResolver.Resolve(field, columnMeta);

            builder.Append("    ");
            WriteIdentifier(builder, field.Name);
            builder
                .Append(' ')
                .Append(type);

            if (i < schema.Fields.Count - 1)
            {
                builder.Append(',');
            }

            builder.AppendLine();
        }

        builder
            .AppendLine(")")
            .Append("ENGINE = ")
            .Append(options.Engine);
    }

    public static string InsertHeader(ClickHouseTableName tableName, DataSchema schema)
    {
        var builder = new StringBuilder();
        WriteInsertHeader(builder, tableName, schema);
        return builder.ToString();
    }

    public static void WriteInsertHeader(StringBuilder builder, ClickHouseTableName tableName, DataSchema schema)
    {
        builder.Append("INSERT INTO ");
        WriteTableNameSql(builder, tableName);
        builder.Append(" (");

        for (var i = 0; i < schema.Fields.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            WriteIdentifier(builder, schema.Fields[i].Name);
        }

        builder.Append(')');
    }

    public static void WriteTableNameSql(StringBuilder builder, ClickHouseTableName tableName)
    {
        if (tableName.Database is not null)
        {
            WriteIdentifier(builder, tableName.Database);
            builder.Append('.');
        }

        WriteIdentifier(builder, tableName.Table);
    }

    public static void WriteIdentifier(StringBuilder builder, string value)
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

    private static DataColumnMeta? FindMeta(DataField field, DataMetaContainer? meta)
    {
        if (meta is null)
        {
            return null;
        }

        return field.Ordinal < meta.Columns.Count
            ? meta.Columns[field.Ordinal]
            : null;
    }
}

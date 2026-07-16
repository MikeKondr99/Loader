using System.Text;

namespace Loader.Core.Writers.ClickHouse;

/// <summary>
/// Явное имя таблицы ClickHouse: опциональная database и обязательная table.
/// Не парсим строку с точками, чтобы политика namespace была видна вызывающему коду.
/// </summary>
public sealed record ClickHouseTableName
{
    public string? Database { get; init; }

    public required string Table { get; init; }

    public string ToSql()
    {
        var builder = new StringBuilder();
        ClickHouseSql.WriteTableNameSql(builder, this);
        return builder.ToString();
    }

    public string ToBulkCopyName()
    {
        if (Database is null)
        {
            return Table;
        }

        var builder = new StringBuilder();
        builder
            .Append(Database)
            .Append('.')
            .Append(Table);
        return builder.ToString();
    }
}

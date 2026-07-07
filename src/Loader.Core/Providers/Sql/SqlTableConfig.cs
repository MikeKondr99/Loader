using Loader.Core.Abstractions;

namespace Loader.Core.Providers.Sql;

/// <summary>
/// Общие настройки чтения SQL-таблицы для DB-провайдеров.
/// </summary>
public sealed record SqlTableConfig : ITableConfig
{
    public required string Sql { get; init; }
}

using System.Data.Common;

namespace Loader.Core.Abstractions;

/// <summary>
/// Базовая метка provider-а, который умеет открыть потоковое чтение из совместимого source.
/// </summary>
public interface IProvider
{
    string Kind { get; }
}

/// <summary>
/// Типизированный provider для конкретной пары source и config.
/// </summary>
/// <typeparam name="TSource">Тип источника, из которого provider умеет читать.</typeparam>
/// <typeparam name="TConfig">Тип настроек конкретного чтения таблицы.</typeparam>
public interface IProvider<in TSource, in TConfig> : IProvider
    where TSource : ISource
    where TConfig : ITableConfig
{
    ValueTask<DbDataReader> OpenReaderAsync(
        TSource source,
        TConfig config,
        CancellationToken cancellationToken = default);
}

namespace Loader.Script.Execution;

/// <summary>
/// Результат физической материализации final table: количество строк и, если собиралась,
/// статистика колонок результата LOAD.
/// </summary>
public sealed record ClickHouseFinalTableMaterialization
{
    public long? RowCount { get; init; }

    public IReadOnlyList<ClickHouseFinalColumnAnalysis>? Columns { get; init; }
}

/// <summary>
/// Статистика одной колонки final table, собранная SQL aggregate-запросом по результату LOAD query.
/// </summary>
public sealed record ClickHouseFinalColumnAnalysis
{
    public required int Ordinal { get; init; }

    public required long NonNullCount { get; init; }

    public required long ApproxDistinctNonNullCount { get; init; }

    public object? Min { get; init; }

    public object? Max { get; init; }
}

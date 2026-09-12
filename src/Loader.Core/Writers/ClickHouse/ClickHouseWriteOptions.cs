namespace Loader.Core.Writers.ClickHouse;

/// <summary>
/// Настройки записи доменного reader-а в ClickHouse.
/// </summary>
public sealed record ClickHouseWriteOptions
{
    public required ClickHouseTableName TableName { get; init; }

    public string Engine { get; init; } = "MergeTree ORDER BY tuple()";

    public bool IfNotExists { get; init; }

    public int BatchSize { get; init; } = 100_000;

    public int MaxDegreeOfParallelism { get; init; } = 1;
}

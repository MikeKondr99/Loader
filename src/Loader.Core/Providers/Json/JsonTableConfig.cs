using Loader.Core.Abstractions;

namespace Loader.Core.Providers.Json;

/// <summary>
/// Настройки чтения JSON-таблицы из массива объектов.
/// </summary>
public sealed record JsonTableConfig : ITableConfig
{
    public required string FileName { get; init; }

    public required IReadOnlyList<string> ArrayPath { get; init; }

    public required JsonTableSchema Schema { get; init; }

    public bool FlattenObjects { get; init; } = true;
}

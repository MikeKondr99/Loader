namespace Loader.Core.Providers.Json;

/// <summary>
/// Явная схема JSON-таблицы.
/// </summary>
public sealed record JsonTableSchema
{
    public required IReadOnlyList<JsonColumnSchema> Columns { get; init; }
}

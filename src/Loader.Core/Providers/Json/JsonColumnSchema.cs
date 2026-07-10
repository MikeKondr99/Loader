namespace Loader.Core.Providers.Json;

/// <summary>
/// Описание одной JSON-колонки: имя результата и путь внутри объекта строки.
/// </summary>
public sealed record JsonColumnSchema
{
    public required string Name { get; init; }

    public required string Path { get; init; }
}

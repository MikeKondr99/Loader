namespace Loader.Core.Providers.Json;

using System.Text;

/// <summary>
/// Предварительно разобранная JSON-колонка для hot path чтения.
/// </summary>
internal sealed record JsonColumnBinding
{
    public required int Ordinal { get; init; }

    public required string Name { get; init; }

    public required string Path { get; init; }

    public required IReadOnlyList<string> Segments { get; init; }

    public required byte[]? FlatUtf8Name { get; init; }

    public bool IsWholeRow => Segments.Count == 0;

    public bool IsFlat => Segments.Count == 1;

    public static JsonColumnBinding FromSchema(int ordinal, JsonColumnSchema column)
    {
        var segments = SplitPath(column.Path);
        return new JsonColumnBinding
        {
            Ordinal = ordinal,
            Name = column.Name,
            Path = column.Path,
            Segments = segments,
            FlatUtf8Name = segments.Count == 1 ? Encoding.UTF8.GetBytes(segments[0]) : null
        };
    }

    private static IReadOnlyList<string> SplitPath(string path)
    {
        if (path.Length == 0)
        {
            return [];
        }

        return path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}

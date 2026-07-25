namespace Loader.Query.Models;

/// <summary>
/// Табличный source одного LOAD-запроса.
/// </summary>
public sealed record QuerySource
{
    public required string Sql { get; init; }

    public required string Alias { get; init; }

    public required IReadOnlyList<Field> Fields { get; init; }
}

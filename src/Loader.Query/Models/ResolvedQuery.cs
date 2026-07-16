namespace Loader.Query.Models;

/// <summary>
/// Один доменный LOAD-запрос после semantic resolve.
/// </summary>
public sealed record ResolvedQuery
{
    public required QuerySource Source { get; init; }

    public required IReadOnlyList<ResolvedSelectItem> Select { get; init; }

    public ResolvedExpression? Where { get; init; }

    public IReadOnlyList<ResolvedExpression> GroupBy { get; init; } = [];

    public IReadOnlyList<ResolvedOrderItem> OrderBy { get; init; } = [];

    public uint? Limit { get; init; }

    public required IReadOnlyList<Field> OutputFields { get; init; }
}

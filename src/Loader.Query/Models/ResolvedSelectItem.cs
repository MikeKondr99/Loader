namespace Loader.Query.Models;

/// <summary>
/// Поле LOAD/SELECT после semantic resolve.
/// </summary>
public sealed record ResolvedSelectItem
{
    public required string Alias { get; init; }

    public required ResolvedExpression Expression { get; init; }

    public required Field OutputField { get; init; }
}

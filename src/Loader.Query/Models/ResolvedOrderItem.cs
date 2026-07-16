namespace Loader.Query.Models;

/// <summary>
/// Поле сортировки после semantic resolve.
/// </summary>
public sealed record ResolvedOrderItem
{
    public required ResolvedExpression Expression { get; init; }

    public required OrderDirection Direction { get; init; }
}

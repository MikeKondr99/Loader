namespace Loader.Query.Resolve;

/// <summary>
/// Результат выбора конкретной перегрузки функции.
/// </summary>
public sealed record FunctionResolution
{
    public required FunctionDefinition Function { get; init; }

    public required IReadOnlyList<FunctionDefinition> Casts { get; init; }

    public required bool ReturnsConst { get; init; }

    public required bool ReturnsAggregated { get; init; }

    public required bool PropagatesNull { get; init; }
}

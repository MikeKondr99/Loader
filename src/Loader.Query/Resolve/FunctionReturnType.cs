using Loader.Query.Models;

namespace Loader.Query.Resolve;

/// <summary>
/// Тип результата функции.
/// </summary>
public sealed record FunctionReturnType
{
    public required DataType DataType { get; init; }

    public required bool CanBeNull { get; init; }

    public bool Aggregated { get; init; }

    public override string ToString()
    {
        return Aggregated
            ? $"agg<{DataType}{(CanBeNull ? string.Empty : "!")}>"
            : $"{DataType}{(CanBeNull ? string.Empty : "!")}";
    }
}

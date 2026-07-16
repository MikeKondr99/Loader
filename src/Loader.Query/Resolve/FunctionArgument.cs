using Loader.Query.Models;

namespace Loader.Query.Resolve;

/// <summary>
/// Аргумент функции.
/// </summary>
public sealed record FunctionArgument
{
    public required string Name { get; init; }

    public required FunctionArgumentType Type { get; init; }

    public required bool PropagateNull { get; init; }

    public bool IsConstRequired { get; init; }

    public override string ToString()
    {
        return $"{Name}: {Type}";
    }
}

/// <summary>
/// Тип аргумента функции.
/// </summary>
public sealed record FunctionArgumentType
{
    public required DataType DataType { get; init; }

    public required bool CanBeNull { get; init; }

    public override string ToString()
    {
        return $"{DataType}{(CanBeNull ? string.Empty : "!")}";
    }
}

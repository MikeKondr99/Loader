namespace Loader.Query.Models;

/// <summary>
/// Результат семантического анализа выражения.
/// </summary>
public readonly record struct ExprType
{
    public required DataType DataType { get; init; }

    public bool CanBeNull { get; init; }

    public bool Aggregated { get; init; }

    public bool IsConstant { get; init; }

    public bool IsLiteral { get; init; }

    public bool IsWindow { get; init; }

    public ExprType Optional()
    {
        return this with
        {
            CanBeNull = true
        };
    }

    public ExprType Aggr()
    {
        return this with
        {
            Aggregated = true
        };
    }

    public ExprType Const()
    {
        return this with
        {
            IsConstant = true
        };
    }

    public ExprType Literal()
    {
        return this with
        {
            IsConstant = true,
            IsLiteral = true
        };
    }

    public override string ToString()
    {
        return $"{DataType}{(CanBeNull ? "?" : string.Empty)}";
    }
}

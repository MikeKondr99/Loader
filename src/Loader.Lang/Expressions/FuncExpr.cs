namespace Loader.Lang.Expressions;

public sealed record FuncExpr : Expr
{
    public required string Name { get; init; }

    public required IReadOnlyList<Expr> Arguments { get; init; }

    public FuncExprKind Kind { get; init; }

    public override string ToString()
    {
        return Kind switch
        {
            FuncExprKind.Binary => $"({Arguments[0]} {Name} {Arguments[1]})",
            FuncExprKind.Unary => $"({Name} {Arguments[0]})",
            FuncExprKind.Method => $"{Arguments[0]}.{Name}({string.Join(", ", Arguments.Skip(1))})",
            FuncExprKind.Default => $"{Name}({string.Join(", ", Arguments)})",
            _ => $"{Name}({string.Join(", ", Arguments)})"
        };
    }

    public override int GetHashCode()
    {
        var value = HashCode.Combine(nameof(FuncExpr), Name, Kind);
        foreach (var argument in Arguments)
        {
            value = HashCode.Combine(value, argument.Hash);
        }

        return value;
    }
}

public enum FuncExprKind
{
    Default = 0,
    Binary = 1,
    Unary = 2,
    Method = 3
}

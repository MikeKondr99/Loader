using Loader.Lang.Expressions;

namespace Loader.Query.Models;

/// <summary>
/// Один доменный LOAD-запрос до semantic resolve.
/// </summary>
public sealed record Query
{
    public required QuerySource Source { get; init; }

    public required IReadOnlyList<SelectItem> Select { get; init; }

    public Expr? Where { get; init; }

    public IReadOnlyList<Expr> GroupBy { get; init; } = [];

    public IReadOnlyList<OrderItem> OrderBy { get; init; } = [];

    public uint? Limit { get; init; }
}

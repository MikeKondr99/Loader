using Loader.Lang.Expressions;

namespace Loader.Query.Models;

/// <summary>
/// Поле сортировки до semantic resolve.
/// </summary>
public sealed record OrderItem
{
    public required Expr Expression { get; init; }

    public required OrderDirection Direction { get; init; }
}

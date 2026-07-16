using Loader.Lang.Expressions;

namespace Loader.Query.Models;

/// <summary>
/// Одно поле в секции LOAD/SELECT до semantic resolve.
/// </summary>
public sealed record SelectItem
{
    public required string Alias { get; init; }

    public required Expr Expression { get; init; }
}

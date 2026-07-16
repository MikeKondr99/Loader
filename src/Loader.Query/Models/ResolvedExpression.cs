using Loader.Lang.Expressions;
using Loader.Query.Template;

namespace Loader.Query.Models;

/// <summary>
/// Выражение после semantic resolve: исходное дерево, SQL-шаблон, тип и resolved-аргументы.
/// </summary>
public sealed record ResolvedExpression
{
    public required Expr Expression { get; init; }

    public required ITemplate Template { get; init; }

    public required ExprType Type { get; init; }

    public IReadOnlyList<ResolvedExpression> Arguments { get; init; } = [];
}

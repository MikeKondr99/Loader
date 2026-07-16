using Loader.Lang.Expressions;
using Loader.Query.Models;

namespace Loader.Query.Resolve;

/// <summary>
/// Сигнатура вызова функции после resolve аргументов.
/// </summary>
public sealed record FunctionSignature
{
    public required string Name { get; init; }

    public required FuncExprKind Kind { get; init; }

    public required IReadOnlyList<ExprType> ArgumentTypes { get; init; }
}

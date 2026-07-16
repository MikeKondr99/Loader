using Loader.Lang.Expressions;
using Loader.Query.Models;
using Loader.Query.Template;

namespace Loader.Query.Resolve;

/// <summary>
/// Минимальное описание функции, достаточное resolver-у для типа и SQL-шаблона.
/// </summary>
public sealed record FunctionDefinition
{
    public required string Name { get; init; }

    public required FuncExprKind Kind { get; init; }

    public required IReadOnlyList<DataType> ArgumentTypes { get; init; }

    public required DataType ReturnType { get; init; }

    public required ITemplate Template { get; init; }

    public bool PropagatesNull { get; init; } = true;

    public bool ReturnsConstant { get; init; }

    public bool ReturnsAggregated { get; init; }
}

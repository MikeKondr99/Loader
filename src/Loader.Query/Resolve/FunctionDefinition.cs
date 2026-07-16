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

    public string? Doc { get; init; }

    public required IReadOnlyList<FunctionArgument> Arguments { get; init; }

    public required FunctionReturnType ReturnType { get; init; }

    public required FuncExprKind Kind { get; init; }

    public required ITemplate Template { get; init; }

    public Func<IReadOnlyList<ResolvedExpression>, ITemplate>? TemplateProvider { get; init; }

    public ImplicitCastMetadata? ImplicitCast { get; init; }

    public Func<IEnumerable<bool>, bool>? CustomNullPropagation { get; init; }

    public required ConstPropagation ConstPropagation { get; init; }

    public override string ToString()
    {
        return Kind is FuncExprKind.Binary
            ? $"({Arguments[0].Type} {Name} {Arguments[1].Type}) -> {ReturnType}"
            : $"{Name}({string.Join(", ", Arguments.Select(static argument => argument.ToString()))}) -> {ReturnType}";
    }
}

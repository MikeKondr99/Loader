using Loader.Lang;
using Loader.Query.Models;

namespace Loader.Query.Resolve;

/// <summary>
/// Контекст одного resolve-прохода по Query.
/// </summary>
public sealed record ResolutionContext
{
    public required QuerySource Source { get; init; }

    public List<Field> Fields { get; init; } = [];

    public required IFunctionResolver Functions { get; init; }

    public ExpressionResolutionContext ExpressionContext { get; init; } = ExpressionResolutionContext.Empty;

    public required List<LangError> Errors { get; init; }
}

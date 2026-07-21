using Loader.Lang;
using Loader.Query.Models;

namespace Loader.Query.Resolve;

/// <summary>
/// Контекст одного resolve-прохода по Query.
/// </summary>
public sealed record ResolutionContext
{
    public required QuerySource Source { get; init; }

    public required IFunctionResolver Functions { get; init; }

    public required List<LangError> Errors { get; init; }
}

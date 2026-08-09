using Loader.Lang.Expressions;

namespace Loader.Lang.Statements;

/// <summary>
/// Одна option из source options.
/// Примеры: <c>path='orders.csv'</c>, <c>delimiter=','</c>, <c>header=true</c>.
/// </summary>
public sealed record LoadOption
{
    /// <summary>
    /// Имя option.
    /// </summary>
    public required string Name { get; init; }

    public required LangSpan Span { get; init; }

    /// <summary>
    /// Literal value после <c>=</c>.
    /// </summary>
    public required Literal Value { get; init; }
}

using Loader.Lang.Expressions;

namespace Loader.Lang.Statements;

/// <summary>
/// Одна option из source options.
/// Примеры: <c>csv</c>, <c>delimiter=','</c>, <c>header=true</c>.
/// </summary>
public sealed record LoadOption
{
    /// <summary>
    /// Имя option.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Literal value после <c>=</c>. Для marker options вроде <c>csv</c> значения нет.
    /// </summary>
    public required Literal? Value { get; init; }
}

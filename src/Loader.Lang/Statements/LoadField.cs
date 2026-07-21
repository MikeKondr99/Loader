using Loader.Lang.Expressions;

namespace Loader.Lang.Statements;

/// <summary>
/// Одно поле LOAD.
/// Пример: <c>amount * 1.2 AS gross_amount</c>.
/// </summary>
public sealed record LoadField
{
    /// <summary>
    /// Имя выходного поля после <c>AS</c>.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Expression, которое будет вычисляться для поля.
    /// </summary>
    public required Expr Expression { get; init; }
}

using Loader.Lang.Expressions;

namespace Loader.Lang.Statements;

/// <summary>
/// Одно поле LOAD.
/// Пример: <c>amount * 1.2 AS gross_amount</c>.
/// </summary>
public sealed record LoadField
{
    /// <summary>
    /// Имя выходного поля после <c>AS</c>. <c>null</c>, если alias нужно вывести на этапе semantic resolution.
    /// </summary>
    public required string? Name { get; init; }

    public required LangSpan Span { get; init; }

    /// <summary>
    /// Expression, которое будет вычисляться для поля.
    /// </summary>
    public required Expr Expression { get; init; }
}

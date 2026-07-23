using Loader.Lang.Expressions;

namespace Loader.Lang.Statements;

/// <summary>
/// Одно поле сортировки из части <c>ORDER BY</c>.
/// Пример: <c>amount DESC</c>.
/// </summary>
public sealed record LoadOrderField
{
    /// <summary>
    /// Expression, по которому выполняется сортировка.
    /// </summary>
    public required Expr Expression { get; init; }

    /// <summary>
    /// Направление сортировки. Если в скрипте оно не указано, используется <c>Ascending</c>.
    /// </summary>
    public required LoadOrderDirection Direction { get; init; }
}

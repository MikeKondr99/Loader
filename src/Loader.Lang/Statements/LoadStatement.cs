using Loader.Lang.Expressions;

namespace Loader.Lang.Statements;

/// <summary>
/// LOAD statement: читает поля из source.
/// Пример: <c>LOAD amount AS amount, city.Lower() AS city FROM [orders.csv] (csv, delimiter=',');</c>
/// </summary>
public sealed record LoadStatement : Statement
{
    /// <summary>
    /// Явно перечисленные поля формы <c>expr AS name</c>.
    /// Если значение <c>null</c>, это форма <c>LOAD *</c>.
    /// </summary>
    public required List<LoadField>? Fields { get; init; }

    /// <summary>
    /// Source из части <c>FROM [source]</c> без квадратных скобок.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Provider/source options из части <c>(csv, delimiter=',')</c>.
    /// </summary>
    public required List<LoadOption> Options { get; init; }

    /// <summary>
    /// Необязательный фильтр строк из части <c>WHERE expr</c>.
    /// </summary>
    public Expr? Where { get; init; }

    /// <summary>
    /// Поля группировки из части <c>GROUP BY</c>. Пустой список означает отсутствие группировки.
    /// </summary>
    public required List<Expr> GroupBy { get; init; }

    /// <summary>
    /// Поля сортировки из части <c>ORDER BY</c>. Пустой список означает отсутствие сортировки.
    /// </summary>
    public required List<LoadOrderField> OrderBy { get; init; }
}

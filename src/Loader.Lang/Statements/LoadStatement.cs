using Loader.Lang.Expressions;

namespace Loader.Lang.Statements;

/// <summary>
/// LOAD statement: читает поля из source.
/// Пример: <c>LOAD amount AS amount, city.Lower() AS city FROM [orders.csv] (csv, delimiter=',');</c>
/// </summary>
public sealed record LoadStatement : Statement
{
    /// <summary>
    /// Имя результирующей таблицы из префикса <c>table_name: LOAD</c>.
    /// <c>null</c> означает, что script execution должен выбрать имя сам.
    /// </summary>
    public string? TableName { get; init; }

    /// <summary>
    /// Явно перечисленные поля формы <c>expr AS name</c>.
    /// Если значение <c>null</c>, это форма <c>LOAD *</c>.
    /// </summary>
    public required List<LoadField>? Fields { get; init; }

    public required LangSpan FromSpan { get; init; }

    public required SourcePart SourcePart { get; init; }

    /// <summary>
    /// Source из части <c>FROM [source]</c> без квадратных скобок.
    /// </summary>
    public string Source => SourcePart.Value;

    /// <summary>
    /// Provider/source options из части <c>(csv, delimiter=',')</c>.
    /// </summary>
    public required List<LoadOption> Options { get; init; }

    /// <summary>
    /// Необязательный фильтр строк из части <c>WHERE expr</c>.
    /// </summary>
    public Expr? Where { get; init; }

    /// <summary>
    /// Поля группировки из части <c>GROUP BY</c>. <c>null</c> означает отсутствие группировки.
    /// </summary>
    public required List<Expr>? GroupBy { get; init; }

    /// <summary>
    /// Поля сортировки из части <c>ORDER BY</c>. <c>null</c> означает отсутствие сортировки.
    /// </summary>
    public required List<LoadOrderField>? OrderBy { get; init; }

    public LimitPart? LimitPart { get; init; }

    /// <summary>
    /// Ограничение количества строк из части <c>LIMIT 100</c>. <c>null</c> означает отсутствие ограничения.
    /// </summary>
    public long? Limit => LimitPart?.Value;

    /// <summary>
    /// Смещение строк из части <c>OFFSET 100</c>. Допускается только после <c>LIMIT</c>.
    /// </summary>
    public long? Offset { get; init; }
}

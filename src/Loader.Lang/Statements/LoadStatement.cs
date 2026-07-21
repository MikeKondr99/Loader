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
}

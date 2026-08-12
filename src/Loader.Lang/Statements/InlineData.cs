using Loader.Lang.Expressions;

namespace Loader.Lang.Statements;

/// <summary>
/// Данные из <c>Inline(col1, col2; value1, value2)</c> внутри source call.
/// </summary>
public sealed record InlineData
{
    /// <summary>
    /// Header-строка Inline: имена колонок.
    /// </summary>
    public required IReadOnlyList<InlineColumn> Columns { get; init; }

    /// <summary>
    /// Строки значений Inline.
    /// </summary>
    public required IReadOnlyList<InlineRow> Rows { get; init; }

    /// <summary>
    /// Span всего inline-блока внутри скобок provider call.
    /// </summary>
    public required LangSpan Span { get; init; }
}

/// <summary>
/// Колонка header-строки Inline.
/// </summary>
public sealed record InlineColumn
{
    /// <summary>
    /// Имя колонки, уже без escaping blocked name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Span имени колонки.
    /// </summary>
    public required LangSpan Span { get; init; }
}

/// <summary>
/// Одна строка значений Inline.
/// </summary>
public sealed record InlineRow
{
    /// <summary>
    /// Literal-значения строки в порядке header-колонок.
    /// </summary>
    public required IReadOnlyList<Literal> Values { get; init; }

    /// <summary>
    /// Span всей строки значений.
    /// </summary>
    public required LangSpan Span { get; init; }
}

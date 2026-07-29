namespace Loader.Lang.Statements;

/// <summary>
/// CALENDAR statement: создает материализованную календарную таблицу.
/// </summary>
public sealed record CalendarStatement : Statement
{
    /// <summary>
    /// Обязательное логическое имя результирующей таблицы.
    /// </summary>
    public required string TableName { get; init; }

    /// <summary>
    /// Источник включительного диапазона дат.
    /// </summary>
    public required CalendarRange Range { get; init; }
}

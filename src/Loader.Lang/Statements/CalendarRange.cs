namespace Loader.Lang.Statements;

/// <summary>
/// Источник включительного диапазона дат для <see cref="CalendarStatement"/>.
/// </summary>
public abstract record CalendarRange;

/// <summary>
/// Явно заданный диапазон дат.
/// </summary>
public sealed record CalendarLiteralRange : CalendarRange
{
    public required DateOnly StartDate { get; init; }

    public required DateOnly EndDate { get; init; }
}

/// <summary>
/// Диапазон, вычисляемый как MIN/MAX поля ранее материализованной таблицы.
/// </summary>
public sealed record CalendarResidentRange : CalendarRange
{
    public required string FieldName { get; init; }

    public required string TableName { get; init; }
}

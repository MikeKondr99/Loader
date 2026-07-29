using CoreDataType = Loader.Core.Models.DataType;

namespace Loader.Script.Execution.Calendar;

internal static class CalendarFieldDefinitions
{
    public static readonly IReadOnlyList<CalendarFieldDefinition> All =
    [
        Date("Date"),
        Integer("Year"),
        Integer("QuarterNumber"),
        Text("Quarter"),
        Integer("YearQuarterNumber"),
        Text("YearQuarter"),
        Integer("MonthNumber"),
        Text("MonthName"),
        Text("MonthShortName"),
        Integer("YearMonthNumber"),
        Text("YearMonth"),
        Text("MonthYear"),
        Integer("WeekNumber"),
        Integer("YearWeek"),
        Date("StartOfWeek"),
        Date("LastDayOfWeek"),
        Integer("DayOfWeek"),
        Text("DayOfWeekName"),
        Integer("DayOfMonth"),
        Integer("DayOfYear"),
        Date("StartOfYear"),
        Date("EndOfYear"),
        Date("StartOfQuarter"),
        Date("EndOfQuarter"),
        Date("StartOfMonth"),
        Date("EndOfMonth"),
        Text("DayMonth"),
        Text("WeekPeriod")
    ];

    private static CalendarFieldDefinition Date(string name)
    {
        return new CalendarFieldDefinition(name, CoreDataType.Date);
    }

    private static CalendarFieldDefinition Integer(string name)
    {
        return new CalendarFieldDefinition(name, CoreDataType.Integer);
    }

    private static CalendarFieldDefinition Text(string name)
    {
        return new CalendarFieldDefinition(name, CoreDataType.Text);
    }
}

internal readonly record struct CalendarFieldDefinition(string Name, CoreDataType DataType);

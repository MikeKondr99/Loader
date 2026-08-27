using System.Globalization;
using System.Text;
using Loader.Core.Writers.ClickHouse;

namespace Loader.Script.Execution;

/// <summary>
/// Строит ClickHouse SQL для provider-а <c>Calendar</c>.
/// Calendar не читает внешний файл: он генерирует диапазон дат и набор производных календарных полей внутри DWH.
/// </summary>
internal static class CalendarSqlBuilder
{
    /// <summary>
    /// Нижняя граница безопасного диапазона. Значение выбрано так, чтобы все calendar expressions
    /// корректно работали с ClickHouse Date/Date32 и ISO-week вычислениями.
    /// </summary>
    public static readonly DateOnly MinSupportedDate = new(1970, 1, 5);

    /// <summary>
    /// Верхняя граница безопасного диапазона для Calendar.
    /// </summary>
    public static readonly DateOnly MaxSupportedDate = new(2148, 12, 31);

    /// <summary>
    /// Доменные имена колонок, которые Calendar всегда возвращает в фиксированном порядке.
    /// </summary>
    public static readonly string[] FieldNames =
    [
        "Date",
        "Year",
        "QuarterNumber",
        "Quarter",
        "YearQuarterNumber",
        "YearQuarter",
        "MonthNumber",
        "MonthName",
        "MonthShortName",
        "YearMonthNumber",
        "YearMonth",
        "MonthYear",
        "WeekNumber",
        "YearWeek",
        "StartOfWeek",
        "LastDayOfWeek",
        "DayOfWeek",
        "DayOfWeekName",
        "DayOfMonth",
        "DayOfYear",
        "StartOfYear",
        "EndOfYear",
        "StartOfQuarter",
        "EndOfQuarter",
        "StartOfMonth",
        "EndOfMonth",
        "DayMonth",
        "WeekPeriod"
    ];

    /// <summary>
    /// Строит calendar SQL для явного диапазона <c>Calendar(min='...', max='...')</c>.
    /// </summary>
    public static string BuildExplicitRangeSql(DateOnly min, DateOnly max)
    {
        var minSql = DateSql(min);
        var maxSql = DateSql(max);
        return BuildCalendarSql($"""
                                 SELECT
                                     {minSql} AS min_date,
                                     {maxSql} AS max_date
                                 """);
    }

    /// <summary>
    /// Строит calendar SQL, где диапазон берется из min/max значения поля уже загруженной таблицы.
    /// Проверка диапазона выполняется внутри ClickHouse, чтобы не вычитывать исходную таблицу в C#.
    /// </summary>
    public static string BuildLoadedTableRangeSql(
        ClickHouseTableName tableName,
        string physicalColumnName)
    {
        var minSupportedDateSql = Date32Sql(MinSupportedDate);
        var maxSupportedDateSql = Date32Sql(MaxSupportedDate);
        var rangeError = EscapeSqlString(
            $"Calendar range must be within {DateLiteral(MinSupportedDate)}..{DateLiteral(MaxSupportedDate)}.");

        return BuildCalendarSql($"""
                                 SELECT
                                     if(
                                         _calendar_range_guard = 0,
                                         if(isNull(min_date32), NULL, toDate(assumeNotNull(min_date32))),
                                         NULL) AS min_date,
                                     if(
                                         _calendar_range_guard = 0,
                                         if(isNull(max_date32), NULL, toDate(assumeNotNull(max_date32))),
                                         NULL) AS max_date
                                 FROM
                                 (
                                     SELECT
                                         min_date32,
                                         max_date32,
                                         throwIf(
                                             (isNotNull(min_date32) AND assumeNotNull(min_date32) < {minSupportedDateSql})
                                             OR (isNotNull(max_date32) AND assumeNotNull(max_date32) > {maxSupportedDateSql}),
                                             '{rangeError}') AS _calendar_range_guard
                                     FROM
                                     (
                                         SELECT
                                             minOrNull(toDate32({Identifier(physicalColumnName)})) AS min_date32,
                                             maxOrNull(toDate32({Identifier(physicalColumnName)})) AS max_date32
                                         FROM {tableName.ToSql()}
                                         WHERE {Identifier(physicalColumnName)} IS NOT NULL
                                     )
                                 )
                                 """);
    }

    private static string BuildCalendarSql(string boundsSql)
    {
        var builder = new StringBuilder();
        builder.AppendLine("SELECT");
        AppendCalendarFields(builder);
        builder.AppendLine();
        builder.AppendLine("FROM");
        builder.AppendLine("(");
        builder.AppendLine("    SELECT addDays(assumeNotNull(min_date), offset) AS d");
        builder.AppendLine("    FROM");
        builder.AppendLine("    (");
        AppendIndented(builder, boundsSql, 8);
        builder.AppendLine("    ) AS bounds");
        builder.AppendLine("    ARRAY JOIN if(");
        builder.AppendLine("        isNull(min_date) OR isNull(max_date),");
        builder.AppendLine("        [],");
        builder.AppendLine("        range(toUInt32(dateDiff('day', assumeNotNull(min_date), assumeNotNull(max_date)) + 1))");
        builder.AppendLine("    ) AS offset");
        builder.Append(')');
        return builder.ToString();
    }

    private static void AppendCalendarFields(StringBuilder builder)
    {
        var fields = new (string Name, string Expression)[]
        {
            ("Date", "d"),
            ("Year", "toYear(d)"),
            ("QuarterNumber", "toQuarter(d)"),
            ("Quarter", "concat('Q', toString(toQuarter(d)))"),
            ("YearQuarterNumber", "(toYear(d) * 10 + toQuarter(d))"),
            ("YearQuarter", "concat(toString(toYear(d)), '-Q', toString(toQuarter(d)))"),
            ("MonthNumber", "toMonth(d)"),
            ("MonthName", "formatDateTime(d, '%M')"),
            ("MonthShortName", "formatDateTime(d, '%b')"),
            ("YearMonthNumber", "toYYYYMM(d)"),
            ("YearMonth", "formatDateTime(d, '%Y-%m')"),
            ("MonthYear", "formatDateTime(d, '%m-%Y')"),
            ("WeekNumber", "toISOWeek(d)"),
            ("YearWeek", "concat(toString(toISOYear(d)), '-W', if(toISOWeek(d) < 10, '0', ''), toString(toISOWeek(d)))"),
            ("StartOfWeek", "toStartOfWeek(d, 1)"),
            ("LastDayOfWeek", "addDays(toStartOfWeek(d, 1), 6)"),
            ("DayOfWeek", "toDayOfWeek(d)"),
            ("DayOfWeekName", "formatDateTime(d, '%W')"),
            ("DayOfMonth", "toDayOfMonth(d)"),
            ("DayOfYear", "toDayOfYear(d)"),
            ("StartOfYear", "toStartOfYear(d)"),
            ("EndOfYear", "addDays(addYears(toStartOfYear(d), 1), -1)"),
            ("StartOfQuarter", "toStartOfQuarter(d)"),
            ("EndOfQuarter", "addDays(addMonths(toStartOfQuarter(d), 3), -1)"),
            ("StartOfMonth", "toStartOfMonth(d)"),
            ("EndOfMonth", "addDays(addMonths(toStartOfMonth(d), 1), -1)"),
            ("DayMonth", "formatDateTime(d, '%d.%m')"),
            ("WeekPeriod", "concat(formatDateTime(toStartOfWeek(d, 1), '%Y-%m-%d'), ' - ', formatDateTime(addDays(toStartOfWeek(d, 1), 6), '%Y-%m-%d'))")
        };

        for (var index = 0; index < fields.Length; index++)
        {
            var field = fields[index];
            builder
                .Append("    ")
                .Append(field.Expression)
                .Append(" AS ")
                .Append(Identifier(field.Name));
            if (index < fields.Length - 1)
            {
                builder.Append(',');
            }

            builder.AppendLine();
        }
    }

    private static void AppendIndented(StringBuilder builder, string text, int spaces)
    {
        var indent = new string(' ', spaces);
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
        {
            builder
                .Append(indent)
                .AppendLine(line);
        }
    }

    private static string DateSql(DateOnly value)
    {
        return $"toDate('{DateLiteral(value)}')";
    }

    private static string Date32Sql(DateOnly value)
    {
        return $"toDate32('{DateLiteral(value)}')";
    }

    private static string DateLiteral(DateOnly value)
    {
        return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string EscapeSqlString(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }

    private static string Identifier(string value)
    {
        var builder = new StringBuilder();
        builder.Append('`');
        foreach (var character in value)
        {
            if (character == '`')
            {
                builder.Append("``");
                continue;
            }

            builder.Append(character);
        }

        builder.Append('`');
        return builder.ToString();
    }
}

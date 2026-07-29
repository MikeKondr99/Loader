using System.Globalization;
using Loader.Core.Writers.ClickHouse;

namespace Loader.Script.Execution.Calendar;

internal static class CalendarSqlBuilder
{
    public static string Build(
        ClickHouseTableName tableName,
        DateOnly startDate,
        DateOnly endDate)
    {
        var start = FormatDate(startDate);
        var end = FormatDate(endDate);

        return $$"""
            CREATE TABLE {{tableName.ToSql()}}
            ENGINE = MergeTree
            ORDER BY `column1`
            AS
            SELECT
                `Date` AS `column1`,
                toYear(`Date`) AS `column2`,
                toQuarter(`Date`) AS `column3`,
                concat(toString(toQuarter(`Date`)), '/Q') AS `column4`,
                toYear(`Date`) * 10 + toQuarter(`Date`) AS `column5`,
                concat(toString(toYear(`Date`)), '-Q', toString(toQuarter(`Date`))) AS `column6`,
                toMonth(`Date`) AS `column7`,
                `MonthName` AS `column8`,
                `MonthShortName` AS `column9`,
                toYear(`Date`) * 100 + toMonth(`Date`) AS `column10`,
                concat(toString(toYear(`Date`)), '-', `MonthShortNameLowerCase`) AS `column11`,
                concat(`MonthShortNameLowerCase`, ' ', toString(toYear(`Date`))) AS `column12`,
                toISOWeek(`Date`) AS `column13`,
                toYearWeek(`Date`, 3) AS `column14`,
                toStartOfWeek(`Date`, 3) AS `column15`,
                addDays(toStartOfWeek(`Date`, 3), 6) AS `column16`,
                toDayOfWeek(`Date`) AS `column17`,
                `DayOfWeekName` AS `column18`,
                toDayOfMonth(`Date`) AS `column19`,
                toDayOfYear(`Date`) AS `column20`,
                toStartOfYear(`Date`) AS `column21`,
                addDays(addYears(toStartOfYear(`Date`), 1), -1) AS `column22`,
                toStartOfQuarter(`Date`) AS `column23`,
                addDays(addQuarters(toStartOfQuarter(`Date`), 1), -1) AS `column24`,
                toStartOfMonth(`Date`) AS `column25`,
                toLastDayOfMonth(`Date`) AS `column26`,
                concat(
                    splitByChar('-', toString(`Date`))[3],
                    '.',
                    splitByChar('-', toString(`Date`))[2]
                ) AS `column27`,
                concat(
                    concat(
                        splitByChar('-', toString(toStartOfWeek(`Date`, 3)))[3],
                        '.',
                        splitByChar('-', toString(toStartOfWeek(`Date`, 3)))[2]
                    ),
                    '-',
                    concat(
                        splitByChar('-', toString(addDays(toStartOfWeek(`Date`, 3), 6)))[3],
                        '.',
                        splitByChar('-', toString(addDays(toStartOfWeek(`Date`, 3), 6)))[2]
                    )
                ) AS `column28`
            FROM
            (
                SELECT
                    `Date`,
                    CASE
                        WHEN toMonth(`Date`) = 1 THEN 'Январь'
                        WHEN toMonth(`Date`) = 2 THEN 'Февраль'
                        WHEN toMonth(`Date`) = 3 THEN 'Март'
                        WHEN toMonth(`Date`) = 4 THEN 'Апрель'
                        WHEN toMonth(`Date`) = 5 THEN 'Май'
                        WHEN toMonth(`Date`) = 6 THEN 'Июнь'
                        WHEN toMonth(`Date`) = 7 THEN 'Июль'
                        WHEN toMonth(`Date`) = 8 THEN 'Август'
                        WHEN toMonth(`Date`) = 9 THEN 'Сентябрь'
                        WHEN toMonth(`Date`) = 10 THEN 'Октябрь'
                        WHEN toMonth(`Date`) = 11 THEN 'Ноябрь'
                        WHEN toMonth(`Date`) = 12 THEN 'Декабрь'
                    END AS `MonthName`,
                    CASE
                        WHEN toMonth(`Date`) = 1 THEN 'Янв'
                        WHEN toMonth(`Date`) = 2 THEN 'Фев'
                        WHEN toMonth(`Date`) = 3 THEN 'Мар'
                        WHEN toMonth(`Date`) = 4 THEN 'Апр'
                        WHEN toMonth(`Date`) = 5 THEN 'Май'
                        WHEN toMonth(`Date`) = 6 THEN 'Июн'
                        WHEN toMonth(`Date`) = 7 THEN 'Июл'
                        WHEN toMonth(`Date`) = 8 THEN 'Авг'
                        WHEN toMonth(`Date`) = 9 THEN 'Сен'
                        WHEN toMonth(`Date`) = 10 THEN 'Окт'
                        WHEN toMonth(`Date`) = 11 THEN 'Ноя'
                        WHEN toMonth(`Date`) = 12 THEN 'Дек'
                    END AS `MonthShortName`,
                    CASE
                        WHEN toMonth(`Date`) = 1 THEN 'янв'
                        WHEN toMonth(`Date`) = 2 THEN 'фев'
                        WHEN toMonth(`Date`) = 3 THEN 'мар'
                        WHEN toMonth(`Date`) = 4 THEN 'апр'
                        WHEN toMonth(`Date`) = 5 THEN 'май'
                        WHEN toMonth(`Date`) = 6 THEN 'июн'
                        WHEN toMonth(`Date`) = 7 THEN 'июл'
                        WHEN toMonth(`Date`) = 8 THEN 'авг'
                        WHEN toMonth(`Date`) = 9 THEN 'сен'
                        WHEN toMonth(`Date`) = 10 THEN 'окт'
                        WHEN toMonth(`Date`) = 11 THEN 'ноя'
                        WHEN toMonth(`Date`) = 12 THEN 'дек'
                    END AS `MonthShortNameLowerCase`,
                    CASE
                        WHEN toDayOfWeek(`Date`) = 1 THEN 'Пн'
                        WHEN toDayOfWeek(`Date`) = 2 THEN 'Вт'
                        WHEN toDayOfWeek(`Date`) = 3 THEN 'Ср'
                        WHEN toDayOfWeek(`Date`) = 4 THEN 'Чт'
                        WHEN toDayOfWeek(`Date`) = 5 THEN 'Пт'
                        WHEN toDayOfWeek(`Date`) = 6 THEN 'Сб'
                        WHEN toDayOfWeek(`Date`) = 7 THEN 'Вс'
                    END AS `DayOfWeekName`
                FROM
                (
                    SELECT addDays(toDate('{{start}}'), number) AS `Date`
                    FROM numbers(
                        toUInt64(dateDiff('day', toDate('{{start}}'), toDate('{{end}}')) + 1)
                    )
                )
            )
            """;
    }

    private static string FormatDate(DateOnly date)
    {
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}

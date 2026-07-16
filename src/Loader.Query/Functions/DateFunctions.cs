using Loader.Query.Models;

namespace Loader.Query.Functions;

public sealed class DateFunctions : FunctionDescriptor
{
    protected override void DefineFunctions()
    {
        Method("Date")
            .Doc("Создает дату по указанному году")
            .Arg("year", DataType.Integer)
            .Returns(DataType.DateTime)
            .Template($"toDateTime(concat(toString({0}), '-01-01 00:00:00'))");

        Method("Date")
            .Doc("Создает дату по году и месяцу")
            .Arg("year", DataType.Integer)
            .Arg("month", DataType.Integer)
            .Returns(DataType.DateTime)
            .Template($"toDateTime(concat(toString({0}), '-', leftPad(toString({1}), 2, '0'), '-01 00:00:00'))");

        Method("Date")
            .Doc("Создает дату по году, месяцу и дню")
            .Arg("year", DataType.Integer)
            .Arg("month", DataType.Integer)
            .Arg("day", DataType.Integer)
            .Returns(DataType.DateTime)
            .Template($"toDateTime(concat(toString({0}), '-', leftPad(toString({1}), 2, '0'), '-', leftPad(toString({2}), 2, '0'), ' 00:00:00'))");

        Method("Date")
            .Doc("Парсит строку как дату")
            .Arg("input", DataType.Text)
            .Returns(DataType.DateTime)
            .Template($"parseDateTimeBestEffortOrNull({0})");

        Method("Now")
            .Doc("Возвращает текущие дату и время")
            .Returns(DataType.DateTime)
            .Template("now()");

        Method("Today")
            .Doc("Возвращает текущую дату")
            .Returns(DataType.DateTime)
            .Template("toStartOfDay(today())");

        Method("AddDays")
            .Doc("Добавляет указанное количество дней к дате")
            .Arg("input", DataType.DateTime)
            .Arg("days", DataType.Integer)
            .Returns(DataType.DateTime)
            .Template($"dateAdd(day, {1}, {0})");
            
        Method("AddMonths")
            .Doc("Добавляет указанное количество месяцев к дате")
            .Arg("input", DataType.DateTime)
            .Arg("months", DataType.Integer)
            .Returns(DataType.DateTime)
            .Template($"dateAdd(month, {1}, {0})");
            
        Method("AddYears")
            .Doc("Добавляет указанное количество лет к дате")
            .Arg("input", DataType.DateTime)
            .Arg("years", DataType.Integer)
            .Returns(DataType.DateTime)
            .Template($"dateAdd(year, {1}, {0})");
            
        Method("Year")
            .Doc("Возвращает год из даты")
            .Arg("input", DataType.DateTime)
            .Returns(DataType.Integer)
            .Template($"toYear({0})");
        Method("Month")
            .Doc("Возвращает месяц из даты")
            .Arg("input", DataType.DateTime)
            .Returns(DataType.Integer)
            .Template($"toMonth({0})");
            
        Method("Day")
            .Doc("Возвращает день месяца из даты")
            .Arg("input", DataType.DateTime)
            .Returns(DataType.Integer)
            .Template($"toDayOfMonth({0})");
            
        Method("Hour")
            .Doc("Возвращает час из даты/времени")
            .Arg("input", DataType.DateTime)
            .Returns(DataType.Integer)
            .Template($"toHour({0})");
            
        Method("Minute")
            .Doc("Возвращает минуты из даты/времени")
            .Arg("input", DataType.DateTime)
            .Returns(DataType.Integer)
            .Template($"toMinute({0})");
            
        Method("Second")
            .Doc("Возвращает секунды из даты/времени")
            .Arg("input", DataType.DateTime)
            .Returns(DataType.Integer)
            .Template($"toSecond({0})");
            
        Method("Quarter")
            .Doc("Возвращает квартал года")
            .Arg("input", DataType.DateTime)
            .Returns(DataType.Integer)
            .Template($"toQuarter({0})");
            
        Method("YearMonth")
            .Doc("Возвращает год и месяц в формате 'YYYY-MM'")
            .Arg("input", DataType.DateTime)
            .Returns(DataType.Text)
            .Template($"formatDateTime({0}, '%Y-%m')");
            
        Method("DateOnly")
            .Doc("Возвращает только дату без времени")
            .Arg("input", DataType.DateTime)
            .Returns(DataType.DateTime)
            .Template($"toDate({0})");
            
        Method("YearQuarter")
            .Doc("Возвращает год и квартал в формате 'YYYY-Q1'")
            .Arg("input", DataType.DateTime)
            .Returns(DataType.Text)
            .Template($"concat(toString(toYear({0})), '-Q', toString(toQuarter({0})))");
            
        Method("YearWeek")
            .Doc("Возвращает год и номер недели в формате 'YYYY-W№'")
            .Arg("input", DataType.DateTime)
            .Returns(DataType.Text)
            .Template($"concat(toString(toISOYear({0})), toString(formatDateTime({0}, '-W%V')))");
            
        Method("DayOfYear")
            .Doc("Возвращает день года")
            .Arg("input", DataType.DateTime)
            .Returns(DataType.Integer)
            .Template($"toDayOfYear({0})");
            
        Method("DayOfWeek")
            .Doc("Возвращает день недели")
            .Arg("input", DataType.DateTime)
            .Returns(DataType.Integer)
            .Template($"toDayOfWeek({0})");
            
        Method("Week")
            .Doc("Возвращает ISO номер недели в году")
            .Arg("input", DataType.DateTime)
            .Returns(DataType.Integer)
            .Template($"toISOWeek({0})");
            
    }
}

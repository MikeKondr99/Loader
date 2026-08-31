using Loader.Query.Models;

namespace Loader.Query.Functions;

public sealed class TimeFunctions : FunctionDescriptor
{
    protected override void DefineFunctions()
    {
        Method("Time")
            .Doc("Парсит текст как время в формате HH:mm:ss. Если значение невалидно, возвращает NULL.")
            .Arg("input", DataType.Text)
            .Returns(DataType.Time)
            .CustomNullPropagation(static _ => true)
            .Template($"parseDateTimeInJodaSyntaxOrNull({0}, 'HH:mm:ss')");

        Method("Time")
            .Doc("Создает время по часу и минуте. Если компоненты вне диапазона, возвращает NULL.")
            .Arg("hour", DataType.Integer)
            .Arg("minute", DataType.Integer)
            .Returns(DataType.Time)
            .CustomNullPropagation(static _ => true)
            .Template($"if(({0} BETWEEN 0 AND 23) AND ({1} BETWEEN 0 AND 59), makeDateTime(1970, 1, 1, {0}, {1}, 0), CAST(NULL AS Nullable(DateTime)))");

        Method("Time")
            .Doc("Создает время по часу, минуте и секунде. Если компоненты вне диапазона, возвращает NULL.")
            .Arg("hour", DataType.Integer)
            .Arg("minute", DataType.Integer)
            .Arg("second", DataType.Integer)
            .Returns(DataType.Time)
            .CustomNullPropagation(static _ => true)
            .Template($"if(({0} BETWEEN 0 AND 23) AND ({1} BETWEEN 0 AND 59) AND ({2} BETWEEN 0 AND 59), makeDateTime(1970, 1, 1, {0}, {1}, {2}), CAST(NULL AS Nullable(DateTime)))");

        // Joda parser в ClickHouse сохраняет последний распарсенный компонент даты.
        // Поэтому фиксированную дату добавляем в конец, чтобы пользовательский date-format
        // не протекал в логический Time и всегда сводился к 1970-01-01.
        Method("Time")
            .Doc("Парсит текст как время по Joda time-only format. Если значение невалидно, возвращает NULL.")
            .Arg("input", DataType.Text)
            .ConstArg("format", DataType.Text)
            .Returns(DataType.Time)
            .CustomNullPropagation(static _ => true)
            .Template($"parseDateTimeInJodaSyntaxOrNull(concat({0}, ' 1970-01-01'), concat({1}, ' yyyy-MM-dd'))");

        Method("Time")
            .Doc("Не производит никаких действий.")
            .Arg("input", DataType.Time)
            .Returns(DataType.Time)
            .Template($"{0}");

        Method("Time")
            .Doc("Извлекает time-only часть из datetime.")
            .Arg("input", DataType.DateTime)
            .Returns(DataType.Time)
            // CH 24.8: datetime - toStartOfDay(datetime) возвращает Int32 секунд,
            // а toTimeWithFixedDate еще недоступен. Текущий физический контракт Time - DateTime('UTC') с датой 1970-01-01.
            // TODO: когда минимальная версия CH будет >= 25.6 и физический Time станет частью контракта,
            // заменить DateTime('UTC')-заглушку на CAST({0}, 'Time').
            .Template($"toDateTime(formatDateTime({0}, '1970-01-01 %H:%i:%S'), 'UTC')");

        Method("Text")
            .Doc("Преобразует время в текст в формате HH:mm:ss.")
            .Arg("input", DataType.Time)
            .Returns(DataType.Text)
            .Template($"formatDateTime({0}, '%H:%i:%S')");

        Method("Text")
            .Doc("Преобразует время в текст по Joda time-only format.")
            .Arg("input", DataType.Time)
            .ConstArg("format", DataType.Text)
            .Returns(DataType.Text)
            .Template($"formatDateTimeInJodaSyntax({0}, {1})");

        Method("Hour")
            .Doc("Возвращает часы из времени.")
            .Arg("input", DataType.Time)
            .Returns(DataType.Integer)
            .Template($"toHour({0})");

        Method("Minute")
            .Doc("Возвращает минуты из времени.")
            .Arg("input", DataType.Time)
            .Returns(DataType.Integer)
            .Template($"toMinute({0})");

        Method("Second")
            .Doc("Возвращает секунды из времени.")
            .Arg("input", DataType.Time)
            .Returns(DataType.Integer)
            .Template($"toSecond({0})");
    }
}

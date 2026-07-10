namespace Loader.Core.Data.AutoCast;

/// <summary>
/// Default-набор кандидатов автокаста. Порядок важен: первый выживший формат выбирается analyzer.
/// </summary>
public static class AutoCastDefaultFormats
{
    public static IReadOnlyList<IAutoCastFormat> Candidates { get; } =
    [
        // Обычное целое число: обрезает пробелы по краям, поддерживает минус, не поддерживает разделители групп.
        Integer(),

        // US number: decimal separator '.', group separator ','.
        Number(decimalSeparator: ".", groupSeparator: ","),

        // RU number: decimal separator ',', group separator ' '.
        Number(decimalSeparator: ",", groupSeparator: " "),

        // Boolean специально не входит в default.
        // В Qlik Sense логический результат это True = -1 и False = 0, но отдельного Bool# format нет.
        // AutoCastFormats.Boolean остается доступным для явной схемы пользователя.

        // ISO date: 2026-01-02.
        Date("yyyy-MM-dd"),

        // RU date: 02.01.2026.
        Date("dd.MM.yyyy"),

        // ISO-like datetime: 2026-01-02 03:04:05.
        DateTime("yyyy-MM-dd HH:mm:ss"),

        // ISO datetime с T-разделителем: 2026-01-02T03:04:05.
        DateTime("yyyy-MM-dd'T'HH:mm:ss"),

        // Время: 03:04:05.
        Time("HH:mm:ss"),

        // TODO: Qlik поддерживает имена месяцев и дней через MonthNames, LongMonthNames, DayNames, LongDayNames.
        // Позже это должно переехать в AutoCast context, например Date("DD MMM YYYY", qlikNames).

        // Fallback: любое значение как текст.
        Text()
    ];

    private static IAutoCastFormat Integer()
    {
        return AutoCastFormats.Integer;
    }

    private static IAutoCastFormat Number(string decimalSeparator, string groupSeparator)
    {
        return AutoCastFormats.Number(decimalSeparator, groupSeparator);
    }

    private static IAutoCastFormat Date(string format)
    {
        return AutoCastFormats.DateExact(format);
    }

    private static IAutoCastFormat DateTime(string format)
    {
        return AutoCastFormats.DateTimeExact(format);
    }

    private static IAutoCastFormat Time(string format)
    {
        return AutoCastFormats.TimeExact(format);
    }

    private static IAutoCastFormat Text()
    {
        return AutoCastFormats.Text;
    }
}

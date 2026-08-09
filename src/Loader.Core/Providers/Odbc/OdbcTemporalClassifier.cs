using System.Text;

namespace Loader.Core.Providers.Odbc;

/// <summary>
/// Вид временного значения, определённый по имени типа ODBC-колонки.
/// </summary>
public enum OdbcTemporalKind
{
    /// <summary>
    /// Колонка не корректируется ODBC temporal adapter-ом.
    /// </summary>
    None,

    /// <summary>
    /// Значение, содержащее только дату.
    /// </summary>
    Date,

    /// <summary>
    /// Значение, содержащее только время без timezone или offset.
    /// </summary>
    Time,

    /// <summary>
    /// Значение времени или timestamp с timezone или offset-семантикой.
    /// </summary>
    TimeZone
}

/// <summary>
/// Классифицирует временные ODBC-колонки по provider type name.
/// ODBC-драйверы часто по-разному отдают CLR-типы date/time, а имя типа сохраняет SQL-форму временного значения.
/// </summary>
public static class OdbcTemporalClassifier
{
    /// <summary>
    /// Классифицирует временной вид колонки по ODBC-метаданным.
    /// </summary>
    /// <param name="typeName">Provider-specific имя типа, возвращённое <c>GetDataTypeName</c>.</param>
    /// <param name="fieldType">CLR-тип, возвращённый <c>GetFieldType</c>; используется как fallback.</param>
    /// <returns>Временной вид, который нужно отдать в общую нормализацию Loader.</returns>
    public static OdbcTemporalKind Classify(string typeName, Type fieldType)
    {
        var normalized = Normalize(typeName);
        var withoutTimeZone = normalized.Contains("withouttimezone", StringComparison.Ordinal);
        if ((!withoutTimeZone && normalized.Contains("withtimezone", StringComparison.Ordinal))
            || normalized.Contains("datetimeoffset", StringComparison.Ordinal)
            || normalized.Contains("timestamptz", StringComparison.Ordinal)
            || normalized.Contains("timetz", StringComparison.Ordinal))
        {
            return OdbcTemporalKind.TimeZone;
        }

        if ((normalized.Contains("timestamp", StringComparison.Ordinal)
             || normalized.Contains("datetime", StringComparison.Ordinal))
            && !normalized.Contains("offset", StringComparison.Ordinal))
        {
            return OdbcTemporalKind.None;
        }

        if (normalized.Contains("date", StringComparison.Ordinal) && !normalized.Contains("time", StringComparison.Ordinal))
        {
            return OdbcTemporalKind.Date;
        }

        if (normalized.Contains("time", StringComparison.Ordinal))
        {
            return OdbcTemporalKind.Time;
        }

        return fieldType == typeof(DateOnly)
            ? OdbcTemporalKind.Date
            : fieldType == typeof(TimeOnly) || fieldType == typeof(TimeSpan)
                ? OdbcTemporalKind.Time
                : OdbcTemporalKind.None;
    }

    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString();
    }
}
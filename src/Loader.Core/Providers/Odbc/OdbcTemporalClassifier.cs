namespace Loader.Core.Providers.Odbc;

public enum OdbcTemporalKind
{
    None,
    Date,
    Time,
    TimeZone
}

public static class OdbcTemporalClassifier
{
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
        return new string(value
            .ToLowerInvariant()
            .Where(static c => char.IsLetterOrDigit(c))
            .ToArray());
    }
}
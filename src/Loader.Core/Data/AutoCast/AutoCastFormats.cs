using System.Globalization;

namespace Loader.Core.Data.AutoCast;

/// <summary>
/// Базовые форматы и фабрики автокаста. Default-набор и порядок находятся в AutoCastDefaultFormats.
/// </summary>
public static class AutoCastFormats
{
    public static IAutoCastFormat Integer { get; } = new AutoCastFormat(
        "Integer",
        DataType.Integer,
        typeof(long),
        static (object value, out object converted) =>
        {
            if (value is long longValue)
            {
                converted = longValue;
                return true;
            }

            if (value is IConvertible &&
                long.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                converted = parsed;
                return true;
            }

            converted = DBNull.Value;
            return false;
        });

    public static IAutoCastFormat InvariantNumber { get; } = Number(decimalSeparator: ".", groupSeparator: ",");

    public static IAutoCastFormat Boolean { get; } = new AutoCastFormat(
        "Boolean",
        DataType.Boolean,
        typeof(bool),
        static (object value, out object converted) =>
        {
            if (value is bool boolean)
            {
                converted = boolean;
                return true;
            }

            if (bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var parsed))
            {
                converted = parsed;
                return true;
            }

            converted = DBNull.Value;
            return false;
        });

    public static IAutoCastFormat Text { get; } = new AutoCastFormat(
        "Text",
        DataType.Text,
        typeof(string),
        static (object value, out object converted) =>
        {
            converted = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            return true;
        });

    public static IAutoCastFormat Number(string decimalSeparator, string groupSeparator)
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberDecimalSeparator = decimalSeparator;
        culture.NumberFormat.NumberGroupSeparator = groupSeparator;

        return new AutoCastFormat(
            $"Number(decimal='{decimalSeparator}', group='{groupSeparator}')",
            DataType.Number,
            typeof(decimal),
            (object value, out object converted) =>
            {
                if (value is decimal decimalValue)
                {
                    converted = decimalValue;
                    return true;
                }

                var text = Convert.ToString(value, CultureInfo.InvariantCulture);
                if (value is IConvertible &&
                    IsValidNumberText(text, decimalSeparator, groupSeparator) &&
                    decimal.TryParse(text, NumberStyles.Number, culture, out var parsed))
                {
                    converted = parsed;
                    return true;
                }

                converted = DBNull.Value;
                return false;
            });
    }

    private static bool IsValidNumberText(string? text, string decimalSeparator, string groupSeparator)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (trimmed.Contains('e', StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var otherDecimalSeparator = decimalSeparator == "." ? "," : ".";
        if (trimmed.Contains(otherDecimalSeparator, StringComparison.Ordinal) &&
            !trimmed.Contains(groupSeparator, StringComparison.Ordinal))
        {
            return false;
        }

        var allowed = "+-0123456789" + decimalSeparator + groupSeparator;
        if (trimmed.Any(character => !allowed.Contains(character, StringComparison.Ordinal)))
        {
            return false;
        }

        var decimalIndex = trimmed.IndexOf(decimalSeparator, StringComparison.Ordinal);
        if (decimalIndex >= 0 &&
            trimmed[(decimalIndex + decimalSeparator.Length)..].Contains(groupSeparator, StringComparison.Ordinal))
        {
            return false;
        }

        var signless = trimmed.TrimStart('+', '-');
        var integerPart = decimalIndex >= 0
            ? signless[..signless.IndexOf(decimalSeparator, StringComparison.Ordinal)]
            : signless;

        return HasValidGroupSeparators(integerPart, groupSeparator);
    }

    private static bool HasValidGroupSeparators(string integerPart, string groupSeparator)
    {
        if (!integerPart.Contains(groupSeparator, StringComparison.Ordinal))
        {
            return true;
        }

        var groups = integerPart.Split(groupSeparator);
        if (groups.Length < 2 || groups[0].Length is < 1 or > 3 || groups[0].Any(static character => !char.IsDigit(character)))
        {
            return false;
        }

        return groups
            .Skip(1)
            .All(static group => group.Length == 3 && group.All(static character => char.IsDigit(character)));
    }

    public static IAutoCastFormat DateExact(string format)
    {
        return new AutoCastFormat(
            $"Date({format})",
            DataType.Date,
            typeof(DateOnly),
            (object value, out object converted) =>
            {
                if (value is DateOnly date)
                {
                    converted = date;
                    return true;
                }

                if (value is global::System.DateTime dateTime)
                {
                    converted = DateOnly.FromDateTime(dateTime);
                    return true;
                }

                if (DateOnly.TryParseExact(Convert.ToString(value, CultureInfo.InvariantCulture), format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                {
                    converted = parsed;
                    return true;
                }

                converted = DBNull.Value;
                return false;
            });
    }

    public static IAutoCastFormat DateTimeExact(string format)
    {
        return new AutoCastFormat(
            $"DateTime({format})",
            DataType.DateTime,
            typeof(DateTime),
            (object value, out object converted) =>
            {
                if (value is global::System.DateTime dateTime)
                {
                    converted = dateTime;
                    return true;
                }

                if (global::System.DateTime.TryParseExact(Convert.ToString(value, CultureInfo.InvariantCulture), format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                {
                    converted = parsed;
                    return true;
                }

                converted = DBNull.Value;
                return false;
            });
    }

    public static IAutoCastFormat TimeExact(string format)
    {
        return new AutoCastFormat(
            $"Time({format})",
            DataType.Time,
            typeof(TimeOnly),
            (object value, out object converted) =>
            {
                if (value is TimeOnly time)
                {
                    converted = time;
                    return true;
                }

                if (value is TimeSpan timeSpan)
                {
                    converted = TimeOnly.FromTimeSpan(timeSpan);
                    return true;
                }

                if (TimeOnly.TryParseExact(Convert.ToString(value, CultureInfo.InvariantCulture), format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                {
                    converted = parsed;
                    return true;
                }

                converted = DBNull.Value;
                return false;
            });
    }

    private sealed class AutoCastFormat(
        string name,
        DataType dataType,
        Type clrType,
        TryConvertValue tryConvert) : IAutoCastFormat
    {
        public string Name => name;

        public DataType DataType => dataType;

        public Type ClrType => clrType;

        public bool TryConvert(object value, out object converted)
        {
            return tryConvert(value, out converted);
        }
    }

    private delegate bool TryConvertValue(object value, out object converted);
}

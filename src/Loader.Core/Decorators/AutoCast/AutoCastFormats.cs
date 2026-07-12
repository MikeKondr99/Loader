using System.Globalization;

namespace Loader.Core.Decorators.AutoCast;

/// <summary>
/// Базовые форматы и фабрики автокаста. AutoCast работает только со строковыми полями.
/// Default-набор и порядок находятся в AutoCastDefaultFormats.
/// </summary>
public static class AutoCastFormats
{
    public static IAutoCastFormat Integer { get; } = new AutoCastFormat(
        "Integer",
        DataType.Integer,
        typeof(long),
        static (string value, out object converted) =>
        {
            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
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
        static (string value, out object converted) =>
        {
            if (bool.TryParse(value, out var parsed))
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
        static (string value, out object converted) =>
        {
            converted = value;
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
            (string value, out object converted) =>
            {
                if (IsValidNumberText(value, decimalSeparator, groupSeparator) &&
                    decimal.TryParse(value, NumberStyles.Number, culture, out var parsed))
                {
                    converted = parsed;
                    return true;
                }

                converted = DBNull.Value;
                return false;
            });
    }

    private static bool IsValidNumberText(string text, string decimalSeparator, string groupSeparator)
    {
        var span = text.AsSpan().Trim();
        if (span.IsEmpty)
        {
            return false;
        }

        var decimalSeparatorChar = decimalSeparator[0];
        var groupSeparatorChar = groupSeparator[0];
        var otherDecimalSeparatorChar = decimalSeparatorChar == '.' ? ',' : '.';

        if (span.Contains('e') || span.Contains('E'))
        {
            return false;
        }

        var hasGroupSeparator = false;
        var hasOtherDecimalSeparator = false;
        for (var i = 0; i < span.Length; i++)
        {
            var character = span[i];
            if (char.IsDigit(character) ||
                character == decimalSeparatorChar ||
                character == groupSeparatorChar ||
                character is '+' or '-')
            {
                hasGroupSeparator |= character == groupSeparatorChar;
                continue;
            }

            if (character == otherDecimalSeparatorChar)
            {
                hasOtherDecimalSeparator = true;
                continue;
            }

            return false;
        }

        if (hasOtherDecimalSeparator && !hasGroupSeparator)
        {
            return false;
        }

        return HasValidNumberShape(span, decimalSeparatorChar, groupSeparatorChar);
    }

    private static bool HasValidNumberShape(ReadOnlySpan<char> value, char decimalSeparator, char groupSeparator)
    {
        var start = value[0] is '+' or '-' ? 1 : 0;
        if (start == value.Length)
        {
            return false;
        }

        var decimalIndex = value[start..].IndexOf(decimalSeparator);
        if (decimalIndex >= 0)
        {
            decimalIndex += start;
            if (value[(decimalIndex + 1)..].Contains(groupSeparator))
            {
                return false;
            }
        }

        var integerEnd = decimalIndex >= 0 ? decimalIndex : value.Length;
        var integerPart = value[start..integerEnd];
        if (integerPart.IsEmpty)
        {
            return false;
        }

        var firstGroupLength = 0;
        var currentGroupLength = 0;
        var seenGroupSeparator = false;
        var groupsAfterFirst = 0;

        for (var i = 0; i < integerPart.Length; i++)
        {
            var character = integerPart[i];
            if (character == groupSeparator)
            {
                if (currentGroupLength == 0)
                {
                    return false;
                }

                if (!seenGroupSeparator)
                {
                    firstGroupLength = currentGroupLength;
                    if (firstGroupLength > 3)
                    {
                        return false;
                    }

                    seenGroupSeparator = true;
                }
                else if (currentGroupLength != 3)
                {
                    return false;
                }

                groupsAfterFirst++;
                currentGroupLength = 0;
                continue;
            }

            if (!char.IsDigit(character))
            {
                return false;
            }

            currentGroupLength++;
        }

        if (!seenGroupSeparator)
        {
            return true;
        }

        return groupsAfterFirst > 0 && currentGroupLength == 3;
    }

    public static IAutoCastFormat DateExact(string format)
    {
        return new AutoCastFormat(
            $"Date({format})",
            DataType.Date,
            typeof(DateOnly),
            (string value, out object converted) =>
            {
                if (DateOnly.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
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
            (string value, out object converted) =>
            {
                if (DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
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
            (string value, out object converted) =>
            {
                if (TimeOnly.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
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

        public bool TryConvert(string value, out object converted)
        {
            return tryConvert(value, out converted);
        }
    }

    private delegate bool TryConvertValue(string value, out object converted);
}

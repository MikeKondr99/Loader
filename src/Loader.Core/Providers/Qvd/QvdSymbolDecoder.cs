using System.Globalization;
using System.Text;

namespace Loader.Core.Providers.Qvd;

internal static class QvdSymbolDecoder
{
    private static readonly DateOnly QvdDateBase = new(1899, 12, 30);

    public static object?[] Decode(ReadOnlySpan<byte> buffer, QvdFieldHeader field)
    {
        var values = new object?[Math.Max(4, Math.Min(buffer.Length, 16))];
        var count = 0;
        var stringStart = 0;
        var awaitingStringTerminator = false;
        object? pendingDualValue = null;
        var hasPendingDualValue = false;
        var i = 0;

        while (i < buffer.Length)
        {
            var currentByte = buffer[i];
            switch (currentByte)
            {
                case 0:
                    if (!awaitingStringTerminator)
                    {
                        i++;
                        break;
                    }

                    var stringValue = Encoding.UTF8.GetString(buffer[stringStart..i]);
                    AddValue(
                        ref values,
                        ref count,
                        hasPendingDualValue
                            ? ResolveDualValue(field, stringValue, pendingDualValue)
                            : stringValue);

                    pendingDualValue = null;
                    hasPendingDualValue = false;
                    awaitingStringTerminator = false;
                    i++;
                    break;

                case 1:
                    QvdLayoutValidator.EnsureAvailableBytes(buffer.Length, i + 1, sizeof(int), "integer");
                    var integerValue = BitConverter.ToInt32(buffer[(i + 1)..(i + 1 + sizeof(int))]);
                    AddValue(
                        ref values,
                        ref count,
                        IsNumberFormat(field, QvdNumberFormatTypes.Date) ? GetDate(integerValue) : integerValue);
                    i += 5;
                    break;

                case 2:
                    QvdLayoutValidator.EnsureAvailableBytes(buffer.Length, i + 1, sizeof(double), "double");
                    var doubleValue = BitConverter.ToDouble(buffer[(i + 1)..(i + 1 + sizeof(double))]);
                    AddValue(ref values, ref count, DecodeDoubleToken(doubleValue, field));
                    i += 9;
                    break;

                case 4:
                    i++;
                    stringStart = i;
                    awaitingStringTerminator = true;
                    break;

                case 5:
                    QvdLayoutValidator.EnsureAvailableBytes(buffer.Length, i + 1, sizeof(int), "dual-string prefix");
                    var dualIntegerValue = BitConverter.ToInt32(buffer[(i + 1)..(i + 1 + sizeof(int))]);
                    pendingDualValue = DecodeDualIntegerToken(dualIntegerValue, field);
                    hasPendingDualValue = pendingDualValue is not null;
                    i += 5;
                    stringStart = i;
                    awaitingStringTerminator = true;
                    break;

                case 6:
                    QvdLayoutValidator.EnsureAvailableBytes(buffer.Length, i + 1, sizeof(double), "dual-string prefix");
                    var dualDoubleValue = BitConverter.ToDouble(buffer[(i + 1)..(i + 1 + sizeof(double))]);
                    pendingDualValue = DecodeDualDoubleToken(dualDoubleValue, field);
                    hasPendingDualValue = pendingDualValue is not null;
                    i += 9;
                    stringStart = i;
                    awaitingStringTerminator = true;
                    break;

                default:
                    i++;
                    break;
            }
        }

        if (awaitingStringTerminator)
        {
            throw new InvalidDataException("QVD symbol section ended with an unterminated string.");
        }

        if (count != values.Length)
        {
            Array.Resize(ref values, count);
        }

        return values;
    }

    private static object DecodeDoubleToken(double value, QvdFieldHeader field)
    {
        return field.NumberFormatType switch
        {
            _ when IsNumberFormat(field, QvdNumberFormatTypes.Time) => GetTime(value),
            _ when IsNumberFormat(field, QvdNumberFormatTypes.Timestamp) => GetDateTime(value),
            _ => value
        };
    }

    private static object? DecodeDualIntegerToken(int value, QvdFieldHeader field)
    {
        return IsNumberFormat(field, QvdNumberFormatTypes.Date)
            ? GetDate(value)
            : null;
    }

    private static object? DecodeDualDoubleToken(double value, QvdFieldHeader field)
    {
        return field.NumberFormatType switch
        {
            _ when IsNumberFormat(field, QvdNumberFormatTypes.Date) => GetDate(value),
            _ when IsNumberFormat(field, QvdNumberFormatTypes.Time) => GetTime(value),
            _ when IsNumberFormat(field, QvdNumberFormatTypes.Timestamp) => GetDateTime(value),
            _ => null
        };
    }

    private static object? ResolveDualValue(QvdFieldHeader field, string stringValue, object? numericValue)
    {
        if (IsNumberFormat(field, QvdNumberFormatTypes.Date) &&
            DateOnly.TryParseExact(stringValue, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        if (IsNumberFormat(field, QvdNumberFormatTypes.Time) &&
            TimeOnly.TryParseExact(stringValue, "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            return time;
        }

        if (IsNumberFormat(field, QvdNumberFormatTypes.Timestamp) &&
            DateTime.TryParseExact(
                stringValue,
                ["yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm:ss'[.000]'"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var timestamp))
        {
            return timestamp;
        }

        return numericValue;
    }

    private static bool IsNumberFormat(QvdFieldHeader field, string expected)
    {
        return string.Equals(field.NumberFormatType, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static void AddValue(ref object?[] values, ref int count, object? value)
    {
        if (count == values.Length)
        {
            Array.Resize(ref values, values.Length * 2);
        }

        values[count] = value;
        count++;
    }

    private static DateOnly GetDate(double date)
    {
        return QvdDateBase.AddDays(Convert.ToInt32(date, CultureInfo.InvariantCulture));
    }

    private static TimeOnly GetTime(double date)
    {
        const long secondsInOneDay = 86400;
        const long ticksInOneSecond = 10000000;
        var ticks = (long)Math.Round(secondsInOneDay * date * ticksInOneSecond, MidpointRounding.AwayFromZero);
        return new TimeOnly(ticks);
    }

    private static DateTime GetDateTime(double date)
    {
        var fractionalPart = date - Math.Floor(date);
        var time = GetTime(fractionalPart);
        var dateOnly = GetDate(Math.Floor(date));
        return new DateTime(dateOnly.Year, dateOnly.Month, dateOnly.Day, time.Hour, time.Minute, time.Second);
    }
}

using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Loader.Core.Models;

internal static class DataValueTextFormatter
{
    private static readonly JsonWriterOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string BinaryHex(byte[] bytes)
    {
        return "\\x" + Convert.ToHexString(bytes);
    }

    public static string Array(Array array)
    {
        return Json(values => WriteArray(values, array.Cast<object?>()));
    }

    public static string ByteArray(byte[] array)
    {
        return Json(values => WriteArray(values, array.Cast<object?>()));
    }

    public static string Tuple(ITuple tuple)
    {
        return Json(values => WriteArray(
            values,
            Enumerable.Range(0, tuple.Length).Select(index => tuple[index])));
    }

    public static string Dictionary(object value)
    {
        if (value is not IDictionary dictionary)
        {
            return ProviderString(value);
        }

        return Json(writer =>
        {
            writer.WriteStartObject();
            foreach (DictionaryEntry entry in dictionary)
            {
                writer.WritePropertyName(Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty);
                WriteValue(writer, entry.Value);
            }

            writer.WriteEndObject();
        });
    }

    public static string ProviderString(object value)
    {
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? value.ToString() ?? string.Empty;
    }

    private static string Json(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, JsonOptions))
        {
            write(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteArray(Utf8JsonWriter writer, IEnumerable<object?> values)
    {
        writer.WriteStartArray();
        foreach (var value in values)
        {
            WriteValue(writer, value);
        }

        writer.WriteEndArray();
    }

    private static void WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
            case DBNull:
                writer.WriteNullValue();
                break;
            case string text:
                writer.WriteStringValue(text);
                break;
            case char character:
                writer.WriteStringValue(character.ToString());
                break;
            case bool boolean:
                writer.WriteBooleanValue(boolean);
                break;
            case byte byteValue:
                writer.WriteNumberValue(byteValue);
                break;
            case sbyte sbyteValue:
                writer.WriteNumberValue(sbyteValue);
                break;
            case short shortValue:
                writer.WriteNumberValue(shortValue);
                break;
            case ushort ushortValue:
                writer.WriteNumberValue(ushortValue);
                break;
            case int intValue:
                writer.WriteNumberValue(intValue);
                break;
            case uint uintValue:
                writer.WriteNumberValue(uintValue);
                break;
            case long longValue:
                writer.WriteNumberValue(longValue);
                break;
            case ulong ulongValue:
                writer.WriteNumberValue(ulongValue);
                break;
            case float floatValue when float.IsFinite(floatValue):
                writer.WriteNumberValue(floatValue);
                break;
            case double doubleValue when double.IsFinite(doubleValue):
                writer.WriteNumberValue(doubleValue);
                break;
            case decimal decimalValue:
                writer.WriteNumberValue(decimalValue);
                break;
            case BigInteger bigInteger:
                writer.WriteRawValue(bigInteger.ToString(CultureInfo.InvariantCulture));
                break;
            case byte[] bytes:
                writer.WriteStringValue(BinaryHex(bytes));
                break;
            case Array array:
                WriteArray(writer, array.Cast<object?>());
                break;
            case ITuple tuple:
                WriteArray(writer, Enumerable.Range(0, tuple.Length).Select(index => tuple[index]));
                break;
            case IDictionary dictionary:
                WriteDictionary(writer, dictionary);
                break;
            case DateTime dateTime:
                writer.WriteStringValue(dateTime.ToString("O", CultureInfo.InvariantCulture));
                break;
            case DateOnly date:
                writer.WriteStringValue(date.ToString("O", CultureInfo.InvariantCulture));
                break;
            case TimeOnly time:
                writer.WriteStringValue(time.ToString("O", CultureInfo.InvariantCulture));
                break;
            case TimeSpan timeSpan:
                writer.WriteStringValue(timeSpan.ToString("c", CultureInfo.InvariantCulture));
                break;
            case DateTimeOffset dateTimeOffset:
                writer.WriteStringValue(dateTimeOffset.ToString("O", CultureInfo.InvariantCulture));
                break;
            default:
                writer.WriteStringValue(ProviderString(value));
                break;
        }
    }

    private static void WriteDictionary(Utf8JsonWriter writer, IDictionary dictionary)
    {
        writer.WriteStartObject();
        foreach (DictionaryEntry entry in dictionary)
        {
            writer.WritePropertyName(Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty);
            WriteValue(writer, entry.Value);
        }

        writer.WriteEndObject();
    }
}

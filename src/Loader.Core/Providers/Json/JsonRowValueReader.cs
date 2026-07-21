using System.Text;
using System.Text.Json;

namespace Loader.Core.Providers.Json;

/// <summary>
/// Общие правила чтения JSON value в текущий доменный контракт JSON provider-а.
/// Все примитивы файла остаются строками, а null превращается в DBNull.Value.
/// </summary>
internal static class JsonRowValueReader
{
    public static object ReadValue(ReadOnlySpan<byte> rowBytes, Utf8JsonReader reader)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString() ?? string.Empty,
            JsonTokenType.Number => GetRawText(rowBytes, reader),
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            JsonTokenType.Null => DBNull.Value,
            JsonTokenType.StartObject => GetRawContainerText(rowBytes, reader),
            JsonTokenType.StartArray => GetRawContainerText(rowBytes, reader),
            _ => DBNull.Value
        };
    }

    private static string GetRawContainerText(ReadOnlySpan<byte> rowBytes, Utf8JsonReader reader)
    {
        var copy = reader;
        copy.Skip();
        return GetRawText(rowBytes, reader, copy.BytesConsumed);
    }

    private static string GetRawText(ReadOnlySpan<byte> rowBytes, Utf8JsonReader reader)
    {
        return GetRawText(rowBytes, reader, reader.BytesConsumed);
    }

    private static string GetRawText(ReadOnlySpan<byte> rowBytes, Utf8JsonReader reader, long bytesConsumed)
    {
        var start = (int)reader.TokenStartIndex;
        var end = (int)bytesConsumed;
        return Encoding.UTF8.GetString(rowBytes[start..end]);
    }
}

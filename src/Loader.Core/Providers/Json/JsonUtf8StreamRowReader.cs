using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Loader.Core.Providers.Json;

/// <summary>
/// Низкоуровневый reader элементов JSON-массива поверх Stream.
///
/// Utf8JsonReader не читает Stream сам: он работает только с уже загруженным span байтов.
/// Поэтому этот класс держит pooled byte-buffer, дочитывает stream кусками, переносит
/// непрочитанный хвост в начало buffer-а и хранит JsonReaderState между проходами.
///
/// Важно: строка таблицы больше не превращается в JsonDocument. Мы гарантируем, что текущий
/// элемент массива целиком находится в byte-buffer, затем вторым Utf8JsonReader-ом разбираем
/// этот slice сразу в object[] значений текущей строки.
/// </summary>
internal sealed class JsonUtf8StreamRowReader : IDisposable
{
    private const int InitialBufferSize = 64 * 1024;

    private readonly Stream _stream;
    private byte[] _buffer;
    private int _bytesInBuffer;
    private bool _isFinalBlock;
    private bool _isDisposed;
    private JsonReaderState _state;
    private int _arrayDepth = -1;

    public JsonUtf8StreamRowReader(Stream stream)
    {
        _stream = stream;
        _buffer = ArrayPool<byte>.Shared.Rent(InitialBufferSize);
    }

    public void MoveToArray(IReadOnlyList<string> arrayPath)
    {
        var contexts = new List<JsonPathContext>();

        while (ReadNavigationToken(out var token))
        {
            // 1. PropertyName запоминаем на текущем объекте: следующий value/container будет его значением.
            if (token.TokenType == JsonTokenType.PropertyName)
            {
                SetCurrentProperty(contexts, token.StringValue);
                continue;
            }

            // 2. На StartArray проверяем путь до этого массива.
            if (token.TokenType == JsonTokenType.StartArray)
            {
                var path = BuildDocumentPath(contexts, CurrentProperty(contexts));
                contexts.Add(new JsonPathContext(CurrentProperty(contexts)));
                ClearCurrentProperty(contexts);

                if (path.SequenceEqual(arrayPath, StringComparer.Ordinal))
                {
                    _arrayDepth = token.Depth;
                    return;
                }

                continue;
            }

            // 3. StartObject нужен только как навигационный контейнер.
            if (token.TokenType == JsonTokenType.StartObject)
            {
                contexts.Add(new JsonPathContext(CurrentProperty(contexts)));
                ClearCurrentProperty(contexts);
                continue;
            }

            // 4. Закрывающие токены убирают контейнер из path stack.
            if (token.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray)
            {
                if (contexts.Count > 0)
                {
                    contexts.RemoveAt(contexts.Count - 1);
                }

                continue;
            }

            // 5. Примитивное значение закрывает текущий property.
            ClearCurrentProperty(contexts);
        }

        throw new InvalidOperationException("JSON array path was not found.");
    }

    public bool ReadNextRow(object[] values, IReadOnlyList<JsonColumnBinding> columns)
    {
        EnsurePositionedOnArray();

        while (true)
        {
            var reader = CreateReader();
            if (!reader.Read())
            {
                if (_isFinalBlock)
                {
                    return false;
                }

                ReadMore();
                continue;
            }

            // 1. EndArray на глубине таблицы означает конец результата.
            if (reader.TokenType == JsonTokenType.EndArray && reader.CurrentDepth == _arrayDepth)
            {
                Consume((int)reader.BytesConsumed, reader.CurrentState);
                return false;
            }

            // 2. Любой другой JSON value внутри массива является строкой таблицы.
            return ReadCurrentValueInto(values, columns);
        }
    }

    public async ValueTask<bool> ReadNextRowAsync(
        object[] values,
        IReadOnlyList<JsonColumnBinding> columns,
        CancellationToken cancellationToken)
    {
        EnsurePositionedOnArray();

        while (true)
        {
            var reader = CreateReader();
            if (!reader.Read())
            {
                if (_isFinalBlock)
                {
                    return false;
                }

                await ReadMoreAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            // 1. EndArray на глубине таблицы означает конец результата.
            if (reader.TokenType == JsonTokenType.EndArray && reader.CurrentDepth == _arrayDepth)
            {
                Consume((int)reader.BytesConsumed, reader.CurrentState);
                return false;
            }

            // 2. Любой другой JSON value внутри массива является строкой таблицы.
            return await ReadCurrentValueIntoAsync(values, columns, cancellationToken).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _stream.Dispose();
        ArrayPool<byte>.Shared.Return(_buffer);
    }

    private bool ReadCurrentValueInto(object[] values, IReadOnlyList<JsonColumnBinding> columns)
    {
        while (true)
        {
            var reader = CreateReader();
            if (!reader.Read())
            {
                if (_isFinalBlock)
                {
                    throw new JsonException("Unexpected end of JSON while reading array item.");
                }

                ReadMore();
                continue;
            }

            if (TryReadBufferedValue(reader, values, columns, out var consumed, out var state))
            {
                Consume(consumed, state);
                return true;
            }

            // 1. Текущий JSON value не поместился в buffer целиком: дочитываем и пробуем снова.
            ReadMore();
        }
    }

    private async ValueTask<bool> ReadCurrentValueIntoAsync(
        object[] values,
        IReadOnlyList<JsonColumnBinding> columns,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var reader = CreateReader();
            if (!reader.Read())
            {
                if (_isFinalBlock)
                {
                    throw new JsonException("Unexpected end of JSON while reading array item.");
                }

                await ReadMoreAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (TryReadBufferedValue(reader, values, columns, out var consumed, out var state))
            {
                Consume(consumed, state);
                return true;
            }

            // 1. Текущий JSON value не поместился в buffer целиком: дочитываем и пробуем снова.
            await ReadMoreAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private bool TryReadBufferedValue(
        Utf8JsonReader firstTokenReader,
        object[] values,
        IReadOnlyList<JsonColumnBinding> columns,
        out int consumed,
        out JsonReaderState state)
    {
        var tokenStart = (int)firstTokenReader.TokenStartIndex;
        var advancingReader = CreateReader();
        advancingReader.Read();

        if (advancingReader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
        {
            if (!advancingReader.TrySkip())
            {
                consumed = 0;
                state = default;
                return false;
            }
        }

        consumed = (int)advancingReader.BytesConsumed;
        state = advancingReader.CurrentState;

        // 1. Разбираем только slice текущего элемента массива, а не весь оставшийся buffer.
        var rowBytes = _buffer.AsSpan(tokenStart, consumed - tokenStart);
        FillValues(rowBytes, values, columns);
        return true;
    }

    private static void FillValues(
        ReadOnlySpan<byte> rowBytes,
        object[] values,
        IReadOnlyList<JsonColumnBinding> columns)
    {
        Array.Fill(values, DBNull.Value);

        var reader = new Utf8JsonReader(rowBytes, isFinalBlock: true, state: default);
        if (!reader.Read())
        {
            return;
        }

        // 1. Пустой path означает весь текущий элемент массива.
        SetWholeRowColumns(rowBytes, reader, values, columns);

        // 2. Обычные dot-path колонки имеют смысл только для объектной строки.
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            return;
        }

        if (CanUseFlatObjectPath(columns))
        {
            ReadFlatObjectColumns(rowBytes, ref reader, values, columns);
            return;
        }

        ReadObjectColumns(rowBytes, ref reader, values, columns);
    }

    private static void ReadFlatObjectColumns(
        ReadOnlySpan<byte> rowBytes,
        ref Utf8JsonReader reader,
        object[] values,
        IReadOnlyList<JsonColumnBinding> columns)
    {
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            // 1. Для flat-схемы сравниваем имя свойства прямо в UTF-8, без string allocation.
            var column = FindFlatColumn(reader, columns);
            if (!reader.Read())
            {
                throw new JsonException("Unexpected end of JSON while reading property value.");
            }

            if (column is not null)
            {
                values[column.Ordinal] = ReadValue(rowBytes, reader);
            }

            // 2. Контейнер нужно продвинуть в основном reader-е, даже если ReadValue уже снял raw text через copy.
            if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
            {
                reader.Skip();
            }
        }
    }

    private static void ReadObjectColumns(
        ReadOnlySpan<byte> rowBytes,
        ref Utf8JsonReader reader,
        object[] values,
        IReadOnlyList<JsonColumnBinding> columns)
    {
        var stack = new List<string>();
        string? propertyName = null;

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.PropertyName:
                    propertyName = reader.GetString() ?? string.Empty;
                    break;

                case JsonTokenType.StartObject:
                    ReadObjectValue(rowBytes, ref reader, values, columns, stack, ref propertyName);
                    break;

                case JsonTokenType.StartArray:
                    ReadArrayValue(rowBytes, ref reader, values, columns, stack, ref propertyName);
                    break;

                case JsonTokenType.EndObject:
                    if (stack.Count == 0)
                    {
                        return;
                    }

                    stack.RemoveAt(stack.Count - 1);
                    break;

                default:
                    ReadPrimitiveValue(rowBytes, reader, values, columns, stack, propertyName);
                    propertyName = null;
                    break;
            }
        }
    }

    private static void ReadObjectValue(
        ReadOnlySpan<byte> rowBytes,
        ref Utf8JsonReader reader,
        object[] values,
        IReadOnlyList<JsonColumnBinding> columns,
        List<string> stack,
        ref string? propertyName)
    {
        if (propertyName is null)
        {
            return;
        }

        // 1. Если сама колонка указывает на объект, возвращаем объект JSON-текстом.
        SetMatchingColumns(rowBytes, reader, values, columns, stack, propertyName);

        // 2. Если есть более глубокие dot-path колонки, продолжаем читать объект.
        if (HasChildColumns(columns, stack, propertyName))
        {
            stack.Add(propertyName);
            propertyName = null;
            return;
        }

        // 3. Иначе объект целиком не нужен: пропускаем его поддерево.
        reader.Skip();
        propertyName = null;
    }

    private static void ReadArrayValue(
        ReadOnlySpan<byte> rowBytes,
        ref Utf8JsonReader reader,
        object[] values,
        IReadOnlyList<JsonColumnBinding> columns,
        List<string> stack,
        ref string? propertyName)
    {
        if (propertyName is not null)
        {
            // 1. Массивы не flatten-ятся, но могут быть явной JSON-текстовой колонкой.
            SetMatchingColumns(rowBytes, reader, values, columns, stack, propertyName);
            propertyName = null;
        }

        // 2. Путь внутрь массива пока не поддерживаем: пропускаем весь массив.
        reader.Skip();
    }

    private static void ReadPrimitiveValue(
        ReadOnlySpan<byte> rowBytes,
        Utf8JsonReader reader,
        object[] values,
        IReadOnlyList<JsonColumnBinding> columns,
        List<string> stack,
        string? propertyName)
    {
        if (propertyName is null)
        {
            return;
        }

        SetMatchingColumns(rowBytes, reader, values, columns, stack, propertyName);
    }

    private static bool CanUseFlatObjectPath(IReadOnlyList<JsonColumnBinding> columns)
    {
        foreach (var column in columns)
        {
            if (!column.IsFlat)
            {
                return false;
            }
        }

        return true;
    }

    private static JsonColumnBinding? FindFlatColumn(Utf8JsonReader reader, IReadOnlyList<JsonColumnBinding> columns)
    {
        foreach (var column in columns)
        {
            if (reader.ValueTextEquals(column.FlatUtf8Name!))
            {
                return column;
            }
        }

        return null;
    }

    private static void SetWholeRowColumns(
        ReadOnlySpan<byte> rowBytes,
        Utf8JsonReader reader,
        object[] values,
        IReadOnlyList<JsonColumnBinding> columns)
    {
        foreach (var column in columns)
        {
            if (column.IsWholeRow)
            {
                values[column.Ordinal] = ReadValue(rowBytes, reader);
            }
        }
    }

    private static void SetMatchingColumns(
        ReadOnlySpan<byte> rowBytes,
        Utf8JsonReader reader,
        object[] values,
        IReadOnlyList<JsonColumnBinding> columns,
        IReadOnlyList<string> stack,
        string propertyName)
    {
        foreach (var column in columns)
        {
            if (!column.IsWholeRow && IsExactPath(column.Segments, stack, propertyName))
            {
                values[column.Ordinal] = ReadValue(rowBytes, reader);
            }
        }
    }

    private static object ReadValue(ReadOnlySpan<byte> rowBytes, Utf8JsonReader reader)
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

    private static bool IsExactPath(IReadOnlyList<string> columnSegments, IReadOnlyList<string> stack, string propertyName)
    {
        if (columnSegments.Count != stack.Count + 1)
        {
            return false;
        }

        for (var i = 0; i < stack.Count; i++)
        {
            if (!string.Equals(columnSegments[i], stack[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return string.Equals(columnSegments[^1], propertyName, StringComparison.Ordinal);
    }

    private static bool HasChildColumns(
        IReadOnlyList<JsonColumnBinding> columns,
        IReadOnlyList<string> stack,
        string propertyName)
    {
        foreach (var column in columns)
        {
            if (column.Segments.Count <= stack.Count + 1)
            {
                continue;
            }

            if (!IsPathPrefix(column.Segments, stack, propertyName))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool IsPathPrefix(IReadOnlyList<string> columnSegments, IReadOnlyList<string> stack, string propertyName)
    {
        for (var i = 0; i < stack.Count; i++)
        {
            if (!string.Equals(columnSegments[i], stack[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return string.Equals(columnSegments[stack.Count], propertyName, StringComparison.Ordinal);
    }

    private bool ReadNavigationToken(out JsonTokenSnapshot token)
    {
        while (true)
        {
            var reader = CreateReader();
            if (reader.Read())
            {
                token = new JsonTokenSnapshot(
                    reader.TokenType,
                    reader.CurrentDepth,
                    reader.TokenType == JsonTokenType.PropertyName ? reader.GetString() ?? string.Empty : string.Empty);

                Consume((int)reader.BytesConsumed, reader.CurrentState);
                return true;
            }

            if (_isFinalBlock)
            {
                token = default;
                return false;
            }

            ReadMore();
        }
    }

    private Utf8JsonReader CreateReader()
    {
        return new Utf8JsonReader(_buffer.AsSpan(0, _bytesInBuffer), _isFinalBlock, _state);
    }

    private void ReadMore()
    {
        EnsureCapacityForMoreBytes();

        var read = _stream.Read(_buffer, _bytesInBuffer, _buffer.Length - _bytesInBuffer);
        _isFinalBlock = read == 0;
        _bytesInBuffer += read;
    }

    private async ValueTask ReadMoreAsync(CancellationToken cancellationToken)
    {
        EnsureCapacityForMoreBytes();

        var read = await _stream
            .ReadAsync(_buffer.AsMemory(_bytesInBuffer, _buffer.Length - _bytesInBuffer), cancellationToken)
            .ConfigureAwait(false);
        _isFinalBlock = read == 0;
        _bytesInBuffer += read;
    }

    private void EnsureCapacityForMoreBytes()
    {
        if (_bytesInBuffer != _buffer.Length)
        {
            return;
        }

        var grown = ArrayPool<byte>.Shared.Rent(_buffer.Length * 2);
        Buffer.BlockCopy(_buffer, 0, grown, 0, _bytesInBuffer);
        ArrayPool<byte>.Shared.Return(_buffer);
        _buffer = grown;
    }

    private void Consume(int consumed, JsonReaderState state)
    {
        var remaining = _bytesInBuffer - consumed;
        if (remaining > 0)
        {
            Buffer.BlockCopy(_buffer, consumed, _buffer, 0, remaining);
        }

        _bytesInBuffer = remaining;
        _state = state;
    }

    private void EnsurePositionedOnArray()
    {
        if (_arrayDepth < 0)
        {
            throw new InvalidOperationException("JSON reader is not positioned on an array.");
        }
    }

    private static IReadOnlyList<string> BuildDocumentPath(List<JsonPathContext> contexts, string? leaf)
    {
        var segments = contexts
            .Where(static context => context.Segment is not null)
            .Select(static context => context.Segment!)
            .ToList();

        if (leaf is not null)
        {
            segments.Add(leaf);
        }

        return segments;
    }

    private static string? CurrentProperty(List<JsonPathContext> contexts)
    {
        return contexts.Count == 0 ? null : contexts[^1].CurrentProperty;
    }

    private static void SetCurrentProperty(List<JsonPathContext> contexts, string propertyName)
    {
        if (contexts.Count == 0)
        {
            contexts.Add(new JsonPathContext(null));
        }

        contexts[^1].CurrentProperty = propertyName;
    }

    private static void ClearCurrentProperty(List<JsonPathContext> contexts)
    {
        if (contexts.Count > 0)
        {
            contexts[^1].CurrentProperty = null;
        }
    }

    private readonly record struct JsonTokenSnapshot(JsonTokenType TokenType, int Depth, string StringValue);

    private sealed class JsonPathContext
    {
        public JsonPathContext(string? segment)
        {
            Segment = segment;
        }

        public string? Segment { get; }

        public string? CurrentProperty { get; set; }
    }
}

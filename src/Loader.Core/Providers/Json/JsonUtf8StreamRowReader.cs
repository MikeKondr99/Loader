using System.Buffers;
using System.Text.Json;

namespace Loader.Core.Providers.Json;

/// <summary>
/// Низкоуровневый reader элементов JSON-массива поверх <see cref="Stream"/>.
///
/// Этот класс решает две разные задачи:
/// 1. Потоково дойти до массива, который задан <c>ArrayPath</c>.
/// 2. После этого читать элементы этого массива по одному.
///
/// Почему здесь есть собственный byte-buffer:
/// <see cref="Utf8JsonReader"/> не умеет сам читать из <see cref="Stream"/>. Он получает span байтов,
/// парсит то, что уже есть в памяти, и возвращает <see cref="JsonReaderState"/>, который можно передать
/// в следующий reader для продолжения. Поэтому мы сами:
/// - читаем bytes из stream;
/// - расширяем buffer, если один JSON-токен или одна строка таблицы больше текущего buffer-а;
/// - переносим непрочитанный хвост buffer-а в начало;
/// - храним JsonReaderState между чтениями.
///
/// Почему строка возвращается как JsonDocument:
/// текущий JsonProviderDataReader уже умеет читать значения по dot-path из <see cref="JsonElement"/>.
/// Чтобы не смешивать два сложных изменения сразу, этот reader материализует только текущий элемент
/// массива-таблицы, а не весь файл. Это сохраняет старую семантику GetRawText для объектов/массивов:
/// форматирование внутри текущего элемента остается таким, каким оно было в JSON.
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

    public JsonDocument? ReadNextRow()
    {
        EnsurePositionedOnArray();

        while (true)
        {
            var reader = CreateReader();
            if (!reader.Read())
            {
                if (_isFinalBlock)
                {
                    return null;
                }

                ReadMore();
                continue;
            }

            // 1. EndArray на глубине таблицы означает конец результата.
            if (reader.TokenType == JsonTokenType.EndArray && reader.CurrentDepth == _arrayDepth)
            {
                Consume((int)reader.BytesConsumed, reader.CurrentState);
                return null;
            }

            // 2. Любой другой JSON value внутри массива является строкой таблицы.
            return ReadCurrentValueAsDocument();
        }
    }

    public async ValueTask<JsonDocument?> ReadNextRowAsync(CancellationToken cancellationToken)
    {
        EnsurePositionedOnArray();

        while (true)
        {
            var reader = CreateReader();
            if (!reader.Read())
            {
                if (_isFinalBlock)
                {
                    return null;
                }

                await ReadMoreAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            // 1. EndArray на глубине таблицы означает конец результата.
            if (reader.TokenType == JsonTokenType.EndArray && reader.CurrentDepth == _arrayDepth)
            {
                Consume((int)reader.BytesConsumed, reader.CurrentState);
                return null;
            }

            // 2. Любой другой JSON value внутри массива является строкой таблицы.
            return await ReadCurrentValueAsDocumentAsync(cancellationToken).ConfigureAwait(false);
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

    private JsonDocument ReadCurrentValueAsDocument()
    {
        while (true)
        {
            var parentReader = CreateReader();
            if (!parentReader.Read())
            {
                if (_isFinalBlock)
                {
                    throw new JsonException("Unexpected end of JSON while reading array item.");
                }

                ReadMore();
                continue;
            }

            var tokenStart = (int)parentReader.TokenStartIndex;
            var valueReader = new Utf8JsonReader(_buffer.AsSpan(tokenStart, _bytesInBuffer - tokenStart), _isFinalBlock, default);

            try
            {
                // 1. Парсим только текущий элемент массива как standalone JSON value.
                var document = JsonDocument.ParseValue(ref valueReader);

                // 2. Отдельным reader-ом продвигаем parent state внутри исходного массива.
                var advancingReader = CreateReader();
                advancingReader.Read();
                if (!advancingReader.TrySkip())
                {
                    ReadMore();
                    document.Dispose();
                    continue;
                }

                // 3. Удаляем из buffer-а байты прочитанного элемента и продолжаем после него.
                Consume((int)advancingReader.BytesConsumed, advancingReader.CurrentState);
                return document;
            }
            catch (JsonException) when (!_isFinalBlock)
            {
                // 4. Значение не поместилось в текущий buffer: дочитываем и пробуем снова.
                ReadMore();
            }
        }
    }

    private async ValueTask<JsonDocument> ReadCurrentValueAsDocumentAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var parentReader = CreateReader();
            if (!parentReader.Read())
            {
                if (_isFinalBlock)
                {
                    throw new JsonException("Unexpected end of JSON while reading array item.");
                }

                await ReadMoreAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            var tokenStart = (int)parentReader.TokenStartIndex;
            var valueReader = new Utf8JsonReader(_buffer.AsSpan(tokenStart, _bytesInBuffer - tokenStart), _isFinalBlock, default);

            try
            {
                // 1. Парсим только текущий элемент массива как standalone JSON value.
                var document = JsonDocument.ParseValue(ref valueReader);

                // 2. Отдельным reader-ом продвигаем parent state внутри исходного массива.
                var advancingReader = CreateReader();
                advancingReader.Read();
                if (!advancingReader.TrySkip())
                {
                    await ReadMoreAsync(cancellationToken).ConfigureAwait(false);
                    document.Dispose();
                    continue;
                }

                // 3. Удаляем из buffer-а байты прочитанного элемента и продолжаем после него.
                Consume((int)advancingReader.BytesConsumed, advancingReader.CurrentState);
                return document;
            }
            catch (JsonException) when (!_isFinalBlock)
            {
                // 4. Значение не поместилось в текущий buffer: дочитываем и пробуем снова.
                await ReadMoreAsync(cancellationToken).ConfigureAwait(false);
            }
        }
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
        if (_bytesInBuffer == _buffer.Length)
        {
            var grown = ArrayPool<byte>.Shared.Rent(_buffer.Length * 2);
            Buffer.BlockCopy(_buffer, 0, grown, 0, _bytesInBuffer);
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = grown;
        }

        var read = _stream.Read(_buffer, _bytesInBuffer, _buffer.Length - _bytesInBuffer);
        _isFinalBlock = read == 0;
        _bytesInBuffer += read;
    }

    private async ValueTask ReadMoreAsync(CancellationToken cancellationToken)
    {
        if (_bytesInBuffer == _buffer.Length)
        {
            var grown = ArrayPool<byte>.Shared.Rent(_buffer.Length * 2);
            Buffer.BlockCopy(_buffer, 0, grown, 0, _bytesInBuffer);
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = grown;
        }

        var read = await _stream
            .ReadAsync(_buffer.AsMemory(_bytesInBuffer, _buffer.Length - _bytesInBuffer), cancellationToken)
            .ConfigureAwait(false);
        _isFinalBlock = read == 0;
        _bytesInBuffer += read;
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

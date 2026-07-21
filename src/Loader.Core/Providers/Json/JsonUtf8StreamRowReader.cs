using System.Buffers;
using System.Text.Json;

namespace Loader.Core.Providers.Json;

/// <summary>
/// Низкоуровневый reader элементов JSON-массива для совместимого dot-path reader-а.
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
    private readonly JsonNestedPathRowReader _rowReader;

    public JsonUtf8StreamRowReader(Stream stream, IReadOnlyList<JsonColumnBinding> columns)
    {
        _stream = stream;
        _buffer = ArrayPool<byte>.Shared.Rent(InitialBufferSize);
        _rowReader = new JsonNestedPathRowReader(columns);
    }

    public void MoveToArray(IReadOnlyList<string> arrayPath)
    {
        var navigator = new JsonArrayPathNavigator(arrayPath);

        while (ReadNavigationToken(out var token))
        {
            // 1. До найденного массива двигаем только навигатор абсолютного JSON path.
            navigator.ProcessToken(token.TokenType, token.Depth, token.StringValue);
            if (navigator.Found)
            {
                _arrayDepth = navigator.ArrayDepth;
                return;
            }
        }

        throw new InvalidOperationException("JSON array path was not found.");
    }

    public bool ReadNextRow(object[] values)
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
            return ReadCurrentValueInto(values);
        }
    }

    public async ValueTask<bool> ReadNextRowAsync(
        object[] values,
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
            return await ReadCurrentValueIntoAsync(values, cancellationToken).ConfigureAwait(false);
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

    private bool ReadCurrentValueInto(object[] values)
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

            if (TryReadBufferedValue(reader, values, out var consumed, out var state))
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

            if (TryReadBufferedValue(reader, values, out var consumed, out var state))
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
        FillValues(rowBytes, values);
        return true;
    }

    private void FillValues(
        ReadOnlySpan<byte> rowBytes,
        object[] values)
    {
        Array.Fill(values, DBNull.Value);

        var reader = new Utf8JsonReader(rowBytes, isFinalBlock: true, state: default);
        if (!reader.Read())
        {
            return;
        }

        // 1. Пустой path означает весь текущий элемент массива.
        _rowReader.SetWholeRowColumns(rowBytes, reader, values);

        // 2. Обычные dot-path колонки имеют смысл только для объектной строки.
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            return;
        }

        _rowReader.Read(rowBytes, ref reader, values);
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
                    reader.TokenType == JsonTokenType.PropertyName ? reader.GetString() ?? string.Empty : null);

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

    private readonly record struct JsonTokenSnapshot(JsonTokenType TokenType, int Depth, string? StringValue);
}

using System.Buffers;
using System.Collections;
using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Loader.Core.Providers.Json;

internal sealed class JsonFastProviderDataReader : DbDataReader
{
    private const int DefaultBatchSize = 1024;

    private readonly string _fileName;
    private readonly JsonUtf8ObjectRowReader _rows;
    private readonly JsonTableSchema _schema;
    private readonly Dictionary<string, int> _ordinals;
    private readonly object[] _batchValues;
    private int _batchRowCount;
    private int _batchRowIndex = -1;
    private bool _endOfRows;
    private bool _hasRow;
    private bool _isClosed;

    private JsonFastProviderDataReader(
        Stream stream,
        string fileName,
        IReadOnlyList<string> arrayPath,
        JsonTableSchema schema)
    {
        _fileName = fileName;
        _rows = new JsonUtf8ObjectRowReader(stream);
        _schema = schema;
        _ordinals = CreateOrdinals(schema);
        _batchValues = CreateEmptyValues(schema.Columns.Count * DefaultBatchSize);

        try
        {
            _rows.MoveToArray(arrayPath);
        }
        catch (InvalidOperationException)
        {
            _rows.Dispose();
            throw new JsonArrayPathNotFoundProviderException(fileName, arrayPath);
        }
    }

    public static bool CanRead(JsonTableSchema schema)
    {
        return schema.Columns
            .Select(static column => column.Path)
            .Distinct(StringComparer.Ordinal)
            .Count() == schema.Columns.Count &&
            schema.Columns.All(static column =>
                column.Path.Length > 0 &&
                !column.Path.Contains('.', StringComparison.Ordinal));
    }

    public static async ValueTask<JsonFastProviderDataReader> CreateAsync(
        Stream stream,
        string fileName,
        IReadOnlyList<string> arrayPath,
        JsonTableSchema schema,
        CancellationToken cancellationToken)
    {
        var reader = new JsonFastProviderDataReader(stream, fileName, arrayPath, schema);
        try
        {
            if (await reader._rows
                    .ReadNextRowAsync(reader._ordinals, reader._batchValues, 0, reader.FieldCount, cancellationToken)
                    .ConfigureAwait(false))
            {
                reader._batchRowCount = 1;
            }
            else
            {
                reader._endOfRows = true;
            }

            return reader;
        }
        catch
        {
            reader.Dispose();
            throw;
        }
    }

    public override object this[int ordinal] => GetValue(ordinal);

    public override object this[string name] => GetValue(GetOrdinal(name));

    public override int Depth => 0;

    public override int FieldCount => _schema.Columns.Count;

    public override bool HasRows => true;

    public override bool IsClosed => _isClosed;

    public override int RecordsAffected => -1;

    public override bool Read()
    {
        try
        {
            return MoveToNextBatchRow() || (!_endOfRows && FillBatch() && MoveToNextBatchRow());
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            throw new JsonFileOpenProviderException(_fileName, ex);
        }
    }

    public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (MoveToNextBatchRow())
            {
                return true;
            }

            return !_endOfRows &&
                   await FillBatchAsync(cancellationToken).ConfigureAwait(false) &&
                   MoveToNextBatchRow();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            throw new JsonFileOpenProviderException(_fileName, ex);
        }
    }

    public override bool NextResult() => false;

    public override object GetValue(int ordinal)
    {
        EnsureReadableRow();
        EnsureOrdinal(ordinal);
        return _batchValues[CurrentBatchOffset() + ordinal];
    }

    public override int GetValues(object[] values)
    {
        var count = Math.Min(values.Length, FieldCount);
        for (var i = 0; i < count; i++)
        {
            values[i] = GetValue(i);
        }

        return count;
    }

    public override string GetName(int ordinal)
    {
        EnsureOrdinal(ordinal);
        return _schema.Columns[ordinal].Name;
    }

    public override int GetOrdinal(string name)
    {
        for (var i = 0; i < _schema.Columns.Count; i++)
        {
            if (string.Equals(_schema.Columns[i].Name, name, StringComparison.Ordinal))
            {
                return i;
            }
        }

        throw new IndexOutOfRangeException($"Column '{name}' was not found.");
    }

    public override string GetDataTypeName(int ordinal)
    {
        EnsureOrdinal(ordinal);
        return "String";
    }

    public override Type GetFieldType(int ordinal)
    {
        EnsureOrdinal(ordinal);
        return typeof(string);
    }

    public override bool IsDBNull(int ordinal) => GetValue(ordinal) == DBNull.Value;

    public override IEnumerator GetEnumerator()
    {
        while (Read())
        {
            yield return this;
        }
    }

    public override bool GetBoolean(int ordinal) => bool.Parse((string)GetValue(ordinal));

    public override byte GetByte(int ordinal) => byte.Parse((string)GetValue(ordinal), CultureInfo.InvariantCulture);

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();

    public override char GetChar(int ordinal) => ((string)GetValue(ordinal))[0];

    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();

    public override DateTime GetDateTime(int ordinal) => DateTime.Parse((string)GetValue(ordinal), CultureInfo.InvariantCulture);

    public override decimal GetDecimal(int ordinal) => decimal.Parse((string)GetValue(ordinal), CultureInfo.InvariantCulture);

    public override double GetDouble(int ordinal) => double.Parse((string)GetValue(ordinal), CultureInfo.InvariantCulture);

    public override float GetFloat(int ordinal) => float.Parse((string)GetValue(ordinal), CultureInfo.InvariantCulture);

    public override Guid GetGuid(int ordinal) => Guid.Parse((string)GetValue(ordinal));

    public override short GetInt16(int ordinal) => short.Parse((string)GetValue(ordinal), CultureInfo.InvariantCulture);

    public override int GetInt32(int ordinal) => int.Parse((string)GetValue(ordinal), CultureInfo.InvariantCulture);

    public override long GetInt64(int ordinal) => long.Parse((string)GetValue(ordinal), CultureInfo.InvariantCulture);

    public override string GetString(int ordinal) => (string)GetValue(ordinal);

    public override void Close()
    {
        _isClosed = true;
        _rows.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Close();
        }

        base.Dispose(disposing);
    }

    private bool MoveToNextBatchRow()
    {
        if (_batchRowIndex + 1 >= _batchRowCount)
        {
            _hasRow = false;
            return false;
        }

        _batchRowIndex++;
        _hasRow = true;
        return true;
    }

    private bool FillBatch()
    {
        ResetBatch();

        while (_batchRowCount < DefaultBatchSize)
        {
            var rowOffset = _batchRowCount * FieldCount;
            if (!_rows.ReadNextRow(_ordinals, _batchValues, rowOffset, FieldCount))
            {
                _endOfRows = true;
                break;
            }

            _batchRowCount++;
        }

        return _batchRowCount > 0;
    }

    private async ValueTask<bool> FillBatchAsync(CancellationToken cancellationToken)
    {
        ResetBatch();

        while (_batchRowCount < DefaultBatchSize)
        {
            var rowOffset = _batchRowCount * FieldCount;
            if (!await _rows.ReadNextRowAsync(_ordinals, _batchValues, rowOffset, FieldCount, cancellationToken).ConfigureAwait(false))
            {
                _endOfRows = true;
                break;
            }

            _batchRowCount++;
        }

        return _batchRowCount > 0;
    }

    private void ResetBatch()
    {
        Array.Fill<object>(_batchValues, DBNull.Value);
        _batchRowCount = 0;
        _batchRowIndex = -1;
        _hasRow = false;
    }

    private int CurrentBatchOffset() => _batchRowIndex * FieldCount;

    private static object[] CreateEmptyValues(int count)
    {
        var values = new object[count];
        Array.Fill<object>(values, DBNull.Value);
        return values;
    }

    private static Dictionary<string, int> CreateOrdinals(JsonTableSchema schema)
    {
        var ordinals = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < schema.Columns.Count; i++)
        {
            ordinals[schema.Columns[i].Path] = i;
        }

        return ordinals;
    }

    private void EnsureReadableRow()
    {
        if (!_hasRow)
        {
            throw new InvalidOperationException("Reader is not positioned on a row.");
        }
    }

    private void EnsureOrdinal(int ordinal)
    {
        if (ordinal < 0 || ordinal >= FieldCount)
        {
            throw new IndexOutOfRangeException($"Column ordinal {ordinal} is out of range.");
        }
    }

    private sealed class JsonUtf8ObjectRowReader : IDisposable
    {
        private const int InitialBufferSize = 64 * 1024;

        private readonly Stream _stream;
        private byte[] _buffer;
        private int _bytesInBuffer;
        private bool _isFinalBlock;
        private bool _isDisposed;
        private JsonReaderState _state;
        private int _arrayDepth = -1;

        public JsonUtf8ObjectRowReader(Stream stream)
        {
            _stream = stream;
            _buffer = ArrayPool<byte>.Shared.Rent(InitialBufferSize);
        }

        public void MoveToArray(IReadOnlyList<string> arrayPath)
        {
            var contexts = new List<JsonPathContext>();

            while (ReadNavigationToken(out var token))
            {
                if (token.TokenType == JsonTokenType.PropertyName)
                {
                    SetCurrentProperty(contexts, token.StringValue);
                    continue;
                }

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

                if (token.TokenType == JsonTokenType.StartObject)
                {
                    contexts.Add(new JsonPathContext(CurrentProperty(contexts)));
                    ClearCurrentProperty(contexts);
                    continue;
                }

                if (token.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray)
                {
                    if (contexts.Count > 0)
                    {
                        contexts.RemoveAt(contexts.Count - 1);
                    }

                    continue;
                }

                ClearCurrentProperty(contexts);
            }

            throw new InvalidOperationException("JSON array path was not found.");
        }

        public bool ReadNextRow(
            IReadOnlyDictionary<string, int> ordinals,
            object[] values,
            int valueOffset,
            int fieldCount)
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

                if (reader.TokenType == JsonTokenType.EndArray && reader.CurrentDepth == _arrayDepth)
                {
                    Consume((int)reader.BytesConsumed, reader.CurrentState);
                    return false;
                }

                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    return ReadNonObjectRow(values, valueOffset, fieldCount);
                }

                return ReadObjectRow(ordinals, values, valueOffset);
            }
        }

        public async ValueTask<bool> ReadNextRowAsync(
            IReadOnlyDictionary<string, int> ordinals,
            object[] values,
            int valueOffset,
            int fieldCount,
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

                if (reader.TokenType == JsonTokenType.EndArray && reader.CurrentDepth == _arrayDepth)
                {
                    Consume((int)reader.BytesConsumed, reader.CurrentState);
                    return false;
                }

                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    return await ReadNonObjectRowAsync(values, valueOffset, fieldCount, cancellationToken).ConfigureAwait(false);
                }

                return await ReadObjectRowAsync(ordinals, values, valueOffset, cancellationToken).ConfigureAwait(false);
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

        private bool ReadObjectRow(IReadOnlyDictionary<string, int> ordinals, object[] values, int valueOffset)
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

                var rowDepth = reader.CurrentDepth;
                while (true)
                {
                    if (!reader.Read())
                    {
                        if (_isFinalBlock)
                        {
                            throw new JsonException("Unexpected end of JSON while reading object row.");
                        }

                        ReadMore();
                        break;
                    }

                    if (reader.TokenType == JsonTokenType.EndObject && reader.CurrentDepth == rowDepth)
                    {
                        Consume((int)reader.BytesConsumed, reader.CurrentState);
                        return true;
                    }

                    if (reader.TokenType != JsonTokenType.PropertyName || reader.CurrentDepth != rowDepth + 1)
                    {
                        continue;
                    }

                    var propertyName = reader.GetString() ?? string.Empty;
                    if (!reader.Read())
                    {
                        if (_isFinalBlock)
                        {
                            throw new JsonException("Unexpected end of JSON while reading property value.");
                        }

                        ReadMore();
                        break;
                    }

                    if (ordinals.TryGetValue(propertyName, out var ordinal))
                    {
                        if (!TryReadValue(ref reader, out var value))
                        {
                            ReadMore();
                            break;
                        }

                        values[valueOffset + ordinal] = value;
                        continue;
                    }

                    if (!reader.TrySkip())
                    {
                        ReadMore();
                        break;
                    }
                }
            }
        }

        private async ValueTask<bool> ReadObjectRowAsync(
            IReadOnlyDictionary<string, int> ordinals,
            object[] values,
            int valueOffset,
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

                var rowDepth = reader.CurrentDepth;
                while (true)
                {
                    if (!reader.Read())
                    {
                        if (_isFinalBlock)
                        {
                            throw new JsonException("Unexpected end of JSON while reading object row.");
                        }

                        await ReadMoreAsync(cancellationToken).ConfigureAwait(false);
                        break;
                    }

                    if (reader.TokenType == JsonTokenType.EndObject && reader.CurrentDepth == rowDepth)
                    {
                        Consume((int)reader.BytesConsumed, reader.CurrentState);
                        return true;
                    }

                    if (reader.TokenType != JsonTokenType.PropertyName || reader.CurrentDepth != rowDepth + 1)
                    {
                        continue;
                    }

                    var propertyName = reader.GetString() ?? string.Empty;
                    if (!reader.Read())
                    {
                        if (_isFinalBlock)
                        {
                            throw new JsonException("Unexpected end of JSON while reading property value.");
                        }

                        await ReadMoreAsync(cancellationToken).ConfigureAwait(false);
                        break;
                    }

                    if (ordinals.TryGetValue(propertyName, out var ordinal))
                    {
                        if (!TryReadValue(ref reader, out var value))
                        {
                            await ReadMoreAsync(cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        values[valueOffset + ordinal] = value;
                        continue;
                    }

                    if (!reader.TrySkip())
                    {
                        await ReadMoreAsync(cancellationToken).ConfigureAwait(false);
                        break;
                    }
                }
            }
        }

        private bool ReadNonObjectRow(object[] values, int valueOffset, int fieldCount)
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

                if (!reader.TrySkip())
                {
                    ReadMore();
                    continue;
                }

                Consume((int)reader.BytesConsumed, reader.CurrentState);
                Array.Fill<object>(values, DBNull.Value, valueOffset, fieldCount);
                return true;
            }
        }

        private async ValueTask<bool> ReadNonObjectRowAsync(
            object[] values,
            int valueOffset,
            int fieldCount,
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

                if (!reader.TrySkip())
                {
                    await ReadMoreAsync(cancellationToken).ConfigureAwait(false);
                    continue;
                }

                Consume((int)reader.BytesConsumed, reader.CurrentState);
                Array.Fill<object>(values, DBNull.Value, valueOffset, fieldCount);
                return true;
            }
        }

        private bool TryReadValue(ref Utf8JsonReader reader, out object value)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Null:
                    value = DBNull.Value;
                    return true;

                case JsonTokenType.String:
                    value = reader.GetString() ?? string.Empty;
                    return true;

                case JsonTokenType.Number:
                    value = Encoding.UTF8.GetString(_buffer, (int)reader.TokenStartIndex, (int)(reader.BytesConsumed - reader.TokenStartIndex));
                    return true;

                case JsonTokenType.True:
                    value = "true";
                    return true;

                case JsonTokenType.False:
                    value = "false";
                    return true;

                case JsonTokenType.StartObject:
                case JsonTokenType.StartArray:
                    var tokenStart = (int)reader.TokenStartIndex;
                    if (!reader.TrySkip())
                    {
                        value = DBNull.Value;
                        return false;
                    }

                    value = Encoding.UTF8.GetString(_buffer, tokenStart, (int)reader.BytesConsumed - tokenStart);
                    return true;

                default:
                    value = DBNull.Value;
                    return true;
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
}
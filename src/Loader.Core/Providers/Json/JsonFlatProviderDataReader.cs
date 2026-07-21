using System.Buffers;
using System.Collections;
using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Loader.Core.Providers.Json;

/// <summary>
/// Быстрый DbDataReader для JSON-таблицы с плоской схемой top-level полей.
///
/// Reader применяется только когда каждый column path непустой и не содержит dot-path.
/// В отличие от совместимого JSON reader-а, он не создает второй Utf8JsonReader на slice строки,
/// а читает свойства объекта напрямую основным stream reader-ом и складывает строки батчами.
/// </summary>
internal sealed class JsonFlatProviderDataReader : DbDataReader
{
    private const int BatchSize = 1024;

    private readonly string _fileName;
    private readonly JsonUtf8FlatObjectStreamReader _rows;
    private readonly JsonTableSchema _schema;
    private readonly JsonColumnBinding[] _columns;
    private readonly object[] _batchValues;
    private int _batchRowCount;
    private int _batchRowIndex = -1;
    private bool _endOfRows;
    private bool _hasRow;
    private bool _isClosed;

    private JsonFlatProviderDataReader(
        Stream stream,
        string fileName,
        IReadOnlyList<string> arrayPath,
        JsonTableSchema schema)
    {
        _fileName = fileName;
        _rows = new JsonUtf8FlatObjectStreamReader(stream);
        _schema = schema;
        _columns = CompileColumns(schema);
        _batchValues = CreateEmptyValues(schema.Columns.Count * BatchSize);

        try
        {
            // 1. Доходим до ArrayPath один раз; дальше reader работает только с элементами массива.
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
        foreach (var column in schema.Columns)
        {
            if (column.Path.Length == 0 || column.Path.Contains('.', StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    public static async ValueTask<JsonFlatProviderDataReader> CreateAsync(
        Stream stream,
        string fileName,
        IReadOnlyList<string> arrayPath,
        JsonTableSchema schema,
        CancellationToken cancellationToken)
    {
        var reader = new JsonFlatProviderDataReader(stream, fileName, arrayPath, schema);
        try
        {
            // 1. Сохраняем контракт: первая ошибка чтения видна уже на OpenReaderAsync.
            if (await reader._rows
                    .ReadNextRowAsync(reader._columns, reader._batchValues, 0, reader.FieldCount, cancellationToken)
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

        while (_batchRowCount < BatchSize)
        {
            var rowOffset = _batchRowCount * FieldCount;
            if (!_rows.ReadNextRow(_columns, _batchValues, rowOffset, FieldCount))
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

        while (_batchRowCount < BatchSize)
        {
            var rowOffset = _batchRowCount * FieldCount;
            if (!await _rows
                    .ReadNextRowAsync(_columns, _batchValues, rowOffset, FieldCount, cancellationToken)
                    .ConfigureAwait(false))
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
        Array.Fill(_batchValues, DBNull.Value);
        _batchRowCount = 0;
        _batchRowIndex = -1;
        _hasRow = false;
    }

    private int CurrentBatchOffset() => _batchRowIndex * FieldCount;

    private static object[] CreateEmptyValues(int count)
    {
        var values = new object[count];
        Array.Fill(values, DBNull.Value);
        return values;
    }

    private static JsonColumnBinding[] CompileColumns(JsonTableSchema schema)
    {
        return schema.Columns
            .Select(static (column, ordinal) => JsonColumnBinding.FromSchema(ordinal, column))
            .ToArray();
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

    private sealed class JsonUtf8FlatObjectStreamReader : IDisposable
    {
        private const int InitialBufferSize = 64 * 1024;

        private readonly Stream _stream;
        private byte[] _buffer;
        private int _bytesInBuffer;
        private bool _isFinalBlock;
        private bool _isDisposed;
        private JsonReaderState _state;
        private int _arrayDepth = -1;

        public JsonUtf8FlatObjectStreamReader(Stream stream)
        {
            _stream = stream;
            _buffer = ArrayPool<byte>.Shared.Rent(InitialBufferSize);
        }

        public void MoveToArray(IReadOnlyList<string> arrayPath)
        {
            var navigator = new JsonArrayPathNavigator(arrayPath);

            while (ReadNavigationToken(out var token))
            {
                // 1. Ищем только массив-таблицу; после него абсолютный путь больше не нужен.
                navigator.ProcessToken(token.TokenType, token.Depth, token.StringValue);
                if (navigator.Found)
                {
                    _arrayDepth = navigator.ArrayDepth;
                    return;
                }
            }

            throw new InvalidOperationException("JSON array path was not found.");
        }

        public bool ReadNextRow(
            IReadOnlyList<JsonColumnBinding> columns,
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

                return ReadObjectRow(columns, values, valueOffset);
            }
        }

        public async ValueTask<bool> ReadNextRowAsync(
            IReadOnlyList<JsonColumnBinding> columns,
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

                return await ReadObjectRowAsync(columns, values, valueOffset, cancellationToken).ConfigureAwait(false);
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

        private bool ReadObjectRow(IReadOnlyList<JsonColumnBinding> columns, object[] values, int valueOffset)
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

                    var column = FindColumn(reader, columns);
                    if (!reader.Read())
                    {
                        if (_isFinalBlock)
                        {
                            throw new JsonException("Unexpected end of JSON while reading property value.");
                        }

                        ReadMore();
                        break;
                    }

                    if (column is not null)
                    {
                        if (!TryReadValue(ref reader, out var value))
                        {
                            ReadMore();
                            break;
                        }

                        values[valueOffset + column.Ordinal] = value;
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
            IReadOnlyList<JsonColumnBinding> columns,
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

                    var column = FindColumn(reader, columns);
                    if (!reader.Read())
                    {
                        if (_isFinalBlock)
                        {
                            throw new JsonException("Unexpected end of JSON while reading property value.");
                        }

                        await ReadMoreAsync(cancellationToken).ConfigureAwait(false);
                        break;
                    }

                    if (column is not null)
                    {
                        if (!TryReadValue(ref reader, out var value))
                        {
                            await ReadMoreAsync(cancellationToken).ConfigureAwait(false);
                            break;
                        }

                        values[valueOffset + column.Ordinal] = value;
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
                Array.Fill(values, DBNull.Value, valueOffset, fieldCount);
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
                Array.Fill(values, DBNull.Value, valueOffset, fieldCount);
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
                    value = Encoding.UTF8.GetString(
                        _buffer,
                        (int)reader.TokenStartIndex,
                        (int)(reader.BytesConsumed - reader.TokenStartIndex));
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

        private static JsonColumnBinding? FindColumn(Utf8JsonReader reader, IReadOnlyList<JsonColumnBinding> columns)
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
}

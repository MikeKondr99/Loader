using System.Collections;
using System.Data.Common;
using System.Globalization;

namespace Loader.Core.Providers.Qvd;

internal sealed class QvdProviderDataReader : DbDataReader
{
    private const int StreamBufferBytes = 4 * 1024 * 1024;
    private readonly byte[] _batchBuffer;
    private readonly QvdFieldReader[] _fieldReaders;
    private readonly QvdFieldSchema[] _fields;
    private readonly QvdHeader _header;
    private readonly int _rowsToRead;
    private readonly Stream _stream;
    private int _batchRowIndex;
    private int _batchRows;
    private object[] _currentValues;
    private bool _hasRow;
    private bool _isClosed;
    private int _rowsRead;

    public QvdProviderDataReader(
        Stream stream,
        QvdHeader header,
        object?[][] symbolsByField)
    {
        _stream = stream;
        _header = header;
        _fields = CreateFieldSchemas(header.Table, symbolsByField);
        _fieldReaders = CreateFieldReaders(header.Table, symbolsByField, _fields);
        _rowsToRead = header.Table.NoOfRecords;
        _batchBuffer = new byte[GetBatchBufferSize(header.Table.RecordByteSize)];
        _currentValues = new object[FieldCount];

        _stream.Seek(GetDataSectionOffset(), SeekOrigin.Begin);
    }

    public override object this[int ordinal] => GetValue(ordinal);

    public override object this[string name] => GetValue(GetOrdinal(name));

    public override int Depth => 0;

    public override int FieldCount => _fields.Length;

    public override bool HasRows => _rowsToRead > 0;

    public override bool IsClosed => _isClosed;

    public override int RecordsAffected => -1;

    public override bool Read()
    {
        try
        {
            if (_rowsRead >= _rowsToRead)
            {
                _hasRow = false;
                return false;
            }

            // 1. Если текущий batch закончился, читаем следующий кусок row section.
            if (_batchRowIndex >= _batchRows)
            {
                ReadNextBatch();
            }

            // 2. Декодируем одну строку из batch buffer в стабильные CLR-значения по схеме.
            var rowBytes = _batchBuffer.AsSpan(
                _batchRowIndex * _header.Table.RecordByteSize,
                _header.Table.RecordByteSize);

            for (var fieldIndex = 0; fieldIndex < _fieldReaders.Length; fieldIndex++)
            {
                _currentValues[fieldIndex] = _fieldReaders[fieldIndex].GetValue(rowBytes);
            }

            _batchRowIndex++;
            _rowsRead++;
            _hasRow = true;
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or OverflowException)
        {
            throw new QvdFormatProviderException(_header.FileName, "Could not read QVD row section.", ex);
        }
    }

    public override bool NextResult() => false;

    public override object GetValue(int ordinal)
    {
        EnsureReadableRow();
        EnsureOrdinal(ordinal);
        return _currentValues[ordinal];
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
        return _fields[ordinal].Name;
    }

    public override int GetOrdinal(string name)
    {
        for (var i = 0; i < _fields.Length; i++)
        {
            if (string.Equals(_fields[i].Name, name, StringComparison.Ordinal))
            {
                return i;
            }
        }

        throw new IndexOutOfRangeException($"Column '{name}' was not found.");
    }

    public override string GetDataTypeName(int ordinal)
    {
        EnsureOrdinal(ordinal);
        return _fields[ordinal].OriginTypeName;
    }

    public override Type GetFieldType(int ordinal)
    {
        EnsureOrdinal(ordinal);
        return _fields[ordinal].ClrType;
    }

    public override bool IsDBNull(int ordinal) => GetValue(ordinal) == DBNull.Value;

    public override IEnumerator GetEnumerator()
    {
        while (Read())
        {
            yield return this;
        }
    }

    public override bool GetBoolean(int ordinal) => (bool)GetValue(ordinal);

    public override byte GetByte(int ordinal) => Convert.ToByte(GetValue(ordinal), CultureInfo.InvariantCulture);

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();

    public override char GetChar(int ordinal) => ((string)GetValue(ordinal))[0];

    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();

    public override DateTime GetDateTime(int ordinal) => (DateTime)GetValue(ordinal);

    public override decimal GetDecimal(int ordinal) => Convert.ToDecimal(GetValue(ordinal), CultureInfo.InvariantCulture);

    public override double GetDouble(int ordinal) => Convert.ToDouble(GetValue(ordinal), CultureInfo.InvariantCulture);

    public override float GetFloat(int ordinal) => Convert.ToSingle(GetValue(ordinal), CultureInfo.InvariantCulture);

    public override Guid GetGuid(int ordinal) => Guid.Parse((string)GetValue(ordinal));

    public override short GetInt16(int ordinal) => Convert.ToInt16(GetValue(ordinal), CultureInfo.InvariantCulture);

    public override int GetInt32(int ordinal) => Convert.ToInt32(GetValue(ordinal), CultureInfo.InvariantCulture);

    public override long GetInt64(int ordinal) => Convert.ToInt64(GetValue(ordinal), CultureInfo.InvariantCulture);

    public override string GetString(int ordinal) => (string)GetValue(ordinal);

    public override void Close()
    {
        if (_isClosed)
        {
            return;
        }

        _isClosed = true;
        _stream.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Close();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (!_isClosed)
        {
            _isClosed = true;
            await _stream.DisposeAsync().ConfigureAwait(false);
        }

        await base.DisposeAsync().ConfigureAwait(false);
    }

    private static QvdFieldSchema[] CreateFieldSchemas(
        QvdTableHeader tableHeader,
        object?[][] symbolsByField)
    {
        var schemas = new QvdFieldSchema[tableHeader.Fields.Count];
        for (var i = 0; i < schemas.Length; i++)
        {
            var field = tableHeader.Fields[i];
            var clrType = InferClrType(symbolsByField[i]);
            schemas[i] = new QvdFieldSchema(
                field.FieldName,
                clrType,
                field.NumberFormatType ?? clrType.Name);
        }

        return schemas;
    }

    private static QvdFieldReader[] CreateFieldReaders(
        QvdTableHeader tableHeader,
        object?[][] symbolsByField,
        QvdFieldSchema[] fieldSchemas)
    {
        var readers = new QvdFieldReader[tableHeader.Fields.Count];
        for (var i = 0; i < readers.Length; i++)
        {
            readers[i] = new QvdFieldReader(tableHeader.Fields[i], symbolsByField[i], fieldSchemas[i].ClrType);
        }

        return readers;
    }

    private static int GetBatchBufferSize(int recordByteSize)
    {
        if (recordByteSize == 0)
        {
            return 0;
        }

        var batchRows = Math.Max(1, StreamBufferBytes / recordByteSize);
        return checked(batchRows * recordByteSize);
    }

    private static Type InferClrType(object?[] symbols)
    {
        Type? type = null;
        foreach (var symbol in symbols)
        {
            if (symbol is null)
            {
                continue;
            }

            var symbolType = symbol.GetType();
            type = MergeTypes(type, symbolType);
            if (type == typeof(string))
            {
                return typeof(string);
            }
        }

        return type ?? typeof(string);
    }

    private static Type MergeTypes(Type? current, Type next)
    {
        if (current is null || current == next)
        {
            return next;
        }

        if ((current == typeof(int) && next == typeof(double)) ||
            (current == typeof(double) && next == typeof(int)))
        {
            return typeof(double);
        }

        return typeof(string);
    }

    private long GetDataSectionOffset()
    {
        return checked(_header.BinarySectionOffset + _header.Table.Offset);
    }

    private void ReadNextBatch()
    {
        var rowsInBatch = Math.Min(
            _batchBuffer.Length / _header.Table.RecordByteSize,
            _rowsToRead - _rowsRead);

        var batchBytes = rowsInBatch * _header.Table.RecordByteSize;
        ReadExactly(_stream, _batchBuffer, batchBytes);
        _batchRows = rowsInBatch;
        _batchRowIndex = 0;
    }

    private static void ReadExactly(Stream stream, byte[] buffer, int bytesToRead)
    {
        var totalRead = 0;
        while (totalRead < bytesToRead)
        {
            var read = stream.Read(buffer, totalRead, bytesToRead - totalRead);
            if (read == 0)
            {
                throw new EndOfStreamException("Unexpected end of QVD row section.");
            }

            totalRead += read;
        }
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

    private readonly record struct QvdFieldSchema(string Name, Type ClrType, string OriginTypeName);

    private readonly struct QvdFieldReader
    {
        private readonly long _bias;
        private readonly int _bitWidth;
        private readonly int _bytesToRead;
        private readonly Type _clrType;
        private readonly object?[] _symbols;
        private readonly int _startBit;
        private readonly int _startByte;

        public QvdFieldReader(QvdFieldHeader field, object?[] symbols, Type clrType)
        {
            if (field.BitWidth > 64)
            {
                throw new InvalidDataException($"QVD field '{field.FieldName}' has unsupported bit width {field.BitWidth}.");
            }

            _startByte = field.BitOffset >> 3;
            _startBit = field.BitOffset & 7;
            _bitWidth = field.BitWidth;
            _bytesToRead = _bitWidth == 0 ? 0 : (_startBit + _bitWidth + 7) >> 3;
            _bias = field.Bias;
            _symbols = symbols;
            _clrType = clrType;
        }

        public object GetValue(ReadOnlySpan<byte> rowBytes)
        {
            var symbolIndex = GetSymbolIndex(rowBytes);
            if (symbolIndex < 0)
            {
                return DBNull.Value;
            }

            if ((ulong)symbolIndex >= (ulong)_symbols.Length)
            {
                throw new InvalidDataException(
                    $"QVD row references symbol index {symbolIndex}, but field contains only {_symbols.Length} symbols.");
            }

            return ConvertSymbol(_symbols[(int)symbolIndex], _clrType);
        }

        private long GetSymbolIndex(ReadOnlySpan<byte> rowBytes)
        {
            if (_bitWidth == 0)
            {
                return _bias;
            }

            UInt128 packedValue = 0;
            for (var byteIndex = 0; byteIndex < _bytesToRead; byteIndex++)
            {
                packedValue |= (UInt128)rowBytes[_startByte + byteIndex] << (byteIndex * 8);
            }

            packedValue >>= _startBit;

            var value = _bitWidth == 64
                ? unchecked((long)(ulong)(packedValue & ulong.MaxValue))
                : (long)(packedValue & ((UInt128.One << _bitWidth) - 1));

            return value + _bias;
        }

        private static object ConvertSymbol(object? value, Type clrType)
        {
            if (value is null)
            {
                return DBNull.Value;
            }

            if (clrType == typeof(string))
            {
                return ConvertSymbolToString(value);
            }

            if (clrType == typeof(double) && value is int integer)
            {
                return (double)integer;
            }

            return value;
        }

        private static string ConvertSymbolToString(object value)
        {
            return value switch
            {
                string text => text,
                DateOnly date => date.ToString("O", CultureInfo.InvariantCulture),
                TimeOnly time => time.ToString("O", CultureInfo.InvariantCulture),
                DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString() ?? string.Empty
            };
        }
    }
}

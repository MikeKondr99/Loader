using System.Collections;
using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace Loader.Core.Providers.Json;

/// <summary>
/// DbDataReader для JSON-таблицы.
///
/// Reader получает явную схему колонок и потоково читает элементы массива-таблицы.
/// В памяти держится byte-buffer низкоуровневого reader-а и object[] значений текущей строки.
/// Строка больше не материализуется как JsonDocument: текущий JSON-элемент парсится напрямую
/// через Utf8JsonReader в буфер значений.
/// </summary>
internal sealed class JsonProviderDataReader : DbDataReader
{
    private readonly string _fileName;
    private readonly JsonUtf8StreamRowReader _rows;
    private readonly JsonTableSchema _schema;
    private readonly JsonColumnBinding[] _columns;
    private object[] _values;
    private object[]? _prefetchedValues;
    private bool _hasPrefetchedRow;
    private bool _prefetchedRowExists;
    private bool _hasRow;
    private bool _isClosed;

    public JsonProviderDataReader(
        Stream stream,
        string fileName,
        IReadOnlyList<string> arrayPath,
        JsonTableSchema schema)
        : this(stream, fileName, arrayPath, schema, prefetchSynchronously: true)
    {
    }

    public static async ValueTask<JsonProviderDataReader> CreateAsync(
        Stream stream,
        string fileName,
        IReadOnlyList<string> arrayPath,
        JsonTableSchema schema,
        CancellationToken cancellationToken)
    {
        var reader = new JsonProviderDataReader(stream, fileName, arrayPath, schema, prefetchSynchronously: false);
        try
        {
            // 1. Сохраняем контракт: ошибки первой строки видны уже на OpenReaderAsync.
            reader._prefetchedValues = new object[reader.FieldCount];
            reader._prefetchedRowExists = await reader._rows
                .ReadNextRowAsync(reader._prefetchedValues, reader._columns, cancellationToken)
                .ConfigureAwait(false);
            reader._hasPrefetchedRow = true;
            return reader;
        }
        catch
        {
            reader.Dispose();
            throw;
        }
    }

    private JsonProviderDataReader(
        Stream stream,
        string fileName,
        IReadOnlyList<string> arrayPath,
        JsonTableSchema schema,
        bool prefetchSynchronously)
    {
        _fileName = fileName;
        _rows = new JsonUtf8StreamRowReader(stream);
        _schema = schema;
        _columns = CompileColumns(schema);
        _values = new object[schema.Columns.Count];

        try
        {
            // 1. На этапе открытия доходим до массива-таблицы, но не читаем его строки.
            _rows.MoveToArray(arrayPath);
        }
        catch (InvalidOperationException)
        {
            _rows.Dispose();
            throw new JsonArrayPathNotFoundProviderException(fileName, arrayPath);
        }

        if (prefetchSynchronously)
        {
            // 2. Sync constructor нужен для полного ADO.NET sync path.
            _prefetchedValues = new object[FieldCount];
            _prefetchedRowExists = _rows.ReadNextRow(_prefetchedValues, _columns);
            _hasPrefetchedRow = true;
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
            if (_hasPrefetchedRow)
            {
                return SetCurrentRowFromPrefetch();
            }

            return SetCurrentRow(_rows.ReadNextRow(_values, _columns));
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
            if (_hasPrefetchedRow)
            {
                return SetCurrentRowFromPrefetch();
            }

            var hasRow = await _rows
                .ReadNextRowAsync(_values, _columns, cancellationToken)
                .ConfigureAwait(false);
            return SetCurrentRow(hasRow);
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
        return _values[ordinal];
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

    private bool SetCurrentRowFromPrefetch()
    {
        // 1. Prefetch хранит уже разобранные значения первой строки, а не JsonDocument.
        _hasPrefetchedRow = false;
        if (!_prefetchedRowExists)
        {
            return SetCurrentRow(false);
        }

        var prefetched = _prefetchedValues!;
        if (_values.Length != prefetched.Length)
        {
            _values = new object[prefetched.Length];
        }

        // 2. Копируем в основной буфер, чтобы дальше GetValue всегда работал с одним массивом.
        Array.Copy(prefetched, _values, prefetched.Length);
        _prefetchedValues = null;
        return SetCurrentRow(true);
    }

    private bool SetCurrentRow(bool hasRow)
    {
        _hasRow = hasRow;
        return hasRow;
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
}

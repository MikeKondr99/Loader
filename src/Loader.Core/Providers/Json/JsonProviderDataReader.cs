using System.Collections;
using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace Loader.Core.Providers.Json;

/// <summary>
/// DbDataReader для JSON-таблицы.
///
/// Reader получает уже явную схему колонок и потоково читает элементы массива-таблицы.
/// В памяти держится только текущий элемент массива как JsonDocument, а не весь JSON-файл.
/// Это компромиссный этап: значения всё еще извлекаются через JsonElement/dot-path, но
/// основной файл больше не материализуется полностью.
/// </summary>
internal sealed class JsonProviderDataReader : DbDataReader
{
    private readonly string _fileName;
    private readonly JsonUtf8StreamRowReader _rows;
    private readonly JsonTableSchema _schema;
    private JsonDocument? _currentRowDocument;
    private JsonDocument? _prefetchedRowDocument;
    private object[] _values;
    private bool _hasPrefetchedRow;
    private bool _hasRow;
    private bool _isClosed;

    public JsonProviderDataReader(
        Stream stream,
        string fileName,
        IReadOnlyList<string> arrayPath,
        JsonTableSchema schema)
    {
        _fileName = fileName;
        _rows = new JsonUtf8StreamRowReader(stream);
        _schema = schema;
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

        // 2. Сохраняем старый контракт: ошибки первой строки видны уже на OpenReaderAsync.
        _prefetchedRowDocument = _rows.ReadNextRow();
        _hasPrefetchedRow = true;
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
            // 1. Освобождаем материализованную строку прошлого Read().
            _currentRowDocument?.Dispose();

            // 2. Берем prefetched строку или читаем следующий элемент массива-таблицы.
            if (_hasPrefetchedRow)
            {
                _currentRowDocument = _prefetchedRowDocument;
                _prefetchedRowDocument = null;
                _hasPrefetchedRow = false;
            }
            else
            {
                _currentRowDocument = _rows.ReadNextRow();
            }

            if (_currentRowDocument is null)
            {
                _hasRow = false;
                return false;
            }

            // 3. Готовим буфер значений под фиксированную схему.
            if (_values.Length != FieldCount)
            {
                _values = new object[FieldCount];
            }

            // 4. Извлекаем значения по dot-path из схемы.
            var row = _currentRowDocument.RootElement;
            for (var i = 0; i < FieldCount; i++)
            {
                _values[i] = ReadValue(row, _schema.Columns[i].Path);
            }

            _hasRow = true;
            return true;
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
        _currentRowDocument?.Dispose();
        _prefetchedRowDocument?.Dispose();
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

    private static object ReadValue(JsonElement row, string path)
    {
        // 1. Ищем значение в объекте строки по dot-path из схемы.
        if (!TryGetByPath(row, path, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return DBNull.Value;
        }

        // 2. Примитивы приводим к строкам; сложные значения оставляем JSON-текстом.
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Object => value.GetRawText(),
            JsonValueKind.Array => value.GetRawText(),
            _ => DBNull.Value
        };
    }

    private static bool TryGetByPath(JsonElement element, string path, out JsonElement value)
    {
        // 1. Пустой path означает весь текущий JSON-элемент.
        value = element;
        if (path.Length == 0)
        {
            return true;
        }

        // 2. Каждый сегмент path должен быть свойством объекта.
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out value))
            {
                return false;
            }
        }

        return true;
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

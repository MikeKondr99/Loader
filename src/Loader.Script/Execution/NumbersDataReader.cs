using System.Collections;
using System.Data;
using System.Data.Common;
using System.Globalization;

namespace Loader.Script.Execution;

internal sealed class NumbersDataReader : DbDataReader
{
    private const string ColumnName = "number";

    private readonly long _max;
    private readonly long _step;
    private long _current;
    private long _next;
    private bool _finished;
    private bool _hasRow;
    private bool _isClosed;

    public NumbersDataReader(long min, long max, long step)
    {
        _max = max;
        _step = step;
        _next = min;
        _finished = min > max;
    }

    public override object this[int ordinal] => GetValue(ordinal);

    public override object this[string name] => GetValue(GetOrdinal(name));

    public override int Depth => 0;

    public override int FieldCount => 1;

    public override bool HasRows => !_finished || _hasRow;

    public override bool IsClosed => _isClosed;

    public override int RecordsAffected => -1;

    public override bool Read()
    {
        if (_finished)
        {
            _hasRow = false;
            return false;
        }

        _current = _next;
        _hasRow = true;

        if (_current > _max - _step)
        {
            _finished = true;
        }
        else
        {
            _next = _current + _step;
        }

        return true;
    }

    public override Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Read());
    }

    public override bool NextResult()
    {
        return false;
    }

    public override object GetValue(int ordinal)
    {
        EnsureReadableRow();
        EnsureOrdinal(ordinal);
        return _current;
    }

    public override int GetValues(object[] values)
    {
        if (values.Length == 0)
        {
            return 0;
        }

        values[0] = GetValue(0);
        return 1;
    }

    public override string GetName(int ordinal)
    {
        EnsureOrdinal(ordinal);
        return ColumnName;
    }

    public override int GetOrdinal(string name)
    {
        if (string.Equals(name, ColumnName, StringComparison.Ordinal))
        {
            return 0;
        }

        throw new IndexOutOfRangeException($"Column '{name}' was not found.");
    }

    public override string GetDataTypeName(int ordinal)
    {
        EnsureOrdinal(ordinal);
        return "Int64";
    }

    public override Type GetFieldType(int ordinal)
    {
        EnsureOrdinal(ordinal);
        return typeof(long);
    }

    public override bool IsDBNull(int ordinal)
    {
        EnsureReadableRow();
        EnsureOrdinal(ordinal);
        return false;
    }

    public override IEnumerator GetEnumerator()
    {
        while (Read())
        {
            yield return this;
        }
    }

    public override bool GetBoolean(int ordinal)
    {
        return GetInt64(ordinal) != 0;
    }

    public override byte GetByte(int ordinal)
    {
        return Convert.ToByte(GetInt64(ordinal), CultureInfo.InvariantCulture);
    }

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
    {
        throw new NotSupportedException();
    }

    public override char GetChar(int ordinal)
    {
        return GetString(ordinal)[0];
    }

    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
    {
        var text = GetString(ordinal);
        if (buffer is null)
        {
            return text.Length;
        }

        var available = Math.Max(0, text.Length - checked((int)dataOffset));
        var count = Math.Min(length, available);
        text.CopyTo(checked((int)dataOffset), buffer, bufferOffset, count);
        return count;
    }

    public override DateTime GetDateTime(int ordinal)
    {
        return new DateTime(GetInt64(ordinal));
    }

    public override decimal GetDecimal(int ordinal)
    {
        return GetInt64(ordinal);
    }

    public override double GetDouble(int ordinal)
    {
        return GetInt64(ordinal);
    }

    public override float GetFloat(int ordinal)
    {
        return GetInt64(ordinal);
    }

    public override Guid GetGuid(int ordinal)
    {
        throw new NotSupportedException();
    }

    public override short GetInt16(int ordinal)
    {
        return Convert.ToInt16(GetInt64(ordinal), CultureInfo.InvariantCulture);
    }

    public override int GetInt32(int ordinal)
    {
        return Convert.ToInt32(GetInt64(ordinal), CultureInfo.InvariantCulture);
    }

    public override long GetInt64(int ordinal)
    {
        EnsureReadableRow();
        EnsureOrdinal(ordinal);
        return _current;
    }

    public override string GetString(int ordinal)
    {
        return GetInt64(ordinal).ToString(CultureInfo.InvariantCulture);
    }

    public override DataTable GetSchemaTable()
    {
        var table = new DataTable("SchemaTable");
        table.Columns.Add(SchemaTableColumn.ColumnName, typeof(string));
        table.Columns.Add(SchemaTableColumn.ColumnOrdinal, typeof(int));
        table.Columns.Add(SchemaTableColumn.DataType, typeof(Type));
        table.Columns.Add(SchemaTableColumn.ProviderType, typeof(int));
        table.Columns.Add(SchemaTableColumn.AllowDBNull, typeof(bool));

        var row = table.NewRow();
        row[SchemaTableColumn.ColumnName] = ColumnName;
        row[SchemaTableColumn.ColumnOrdinal] = 0;
        row[SchemaTableColumn.DataType] = typeof(long);
        row[SchemaTableColumn.ProviderType] = 0;
        row[SchemaTableColumn.AllowDBNull] = false;
        table.Rows.Add(row);
        return table;
    }

    public override void Close()
    {
        _isClosed = true;
        _hasRow = false;
        _finished = true;
    }

    private void EnsureReadableRow()
    {
        if (!_hasRow)
        {
            throw new InvalidOperationException("Reader is not positioned on a row.");
        }
    }

    private static void EnsureOrdinal(int ordinal)
    {
        if (ordinal != 0)
        {
            throw new IndexOutOfRangeException($"Column ordinal {ordinal} is out of range.");
        }
    }
}

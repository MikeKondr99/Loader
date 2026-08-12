using System.Collections;
using System.Data;
using System.Data.Common;
using System.Globalization;
using Loader.Lang.Expressions;
using Loader.Lang.Statements;

namespace Loader.Script.Execution;

/// <summary>
/// DbDataReader поверх Inline literal-данных; типы колонок выводятся по всем строкам до чтения.
/// </summary>
internal sealed class InlineDataReader : DbDataReader
{
    private readonly string[] _names;
    private readonly Type[] _types;
    private readonly bool[] _nullable;
    private readonly object?[][] _rows;
    private int _rowIndex = -1;
    private bool _isClosed;

    public InlineDataReader(InlineData data)
    {
        _names = data.Columns.Select(static column => column.Name).ToArray();
        _types = InferTypes(data);
        _nullable = InferNullability(data);
        _rows = data.Rows
            .Select(row => row.Values.Select((value, ordinal) => ConvertLiteral(value, _types[ordinal])).ToArray())
            .ToArray();
    }

    public override object this[int ordinal] => GetValue(ordinal);

    public override object this[string name] => GetValue(GetOrdinal(name));

    public override int Depth => 0;

    public override int FieldCount => _names.Length;

    public override bool HasRows => _rows.Length > 0;

    public override bool IsClosed => _isClosed;

    public override int RecordsAffected => -1;

    public override bool Read()
    {
        if (_rowIndex + 1 >= _rows.Length)
        {
            return false;
        }

        _rowIndex++;
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
        return _rows[_rowIndex][ordinal] ?? DBNull.Value;
    }

    public override int GetValues(object[] values)
    {
        EnsureReadableRow();
        var count = Math.Min(values.Length, FieldCount);
        for (var ordinal = 0; ordinal < count; ordinal++)
        {
            values[ordinal] = GetValue(ordinal);
        }

        return count;
    }

    public override string GetName(int ordinal)
    {
        EnsureOrdinal(ordinal);
        return _names[ordinal];
    }

    public override int GetOrdinal(string name)
    {
        for (var ordinal = 0; ordinal < _names.Length; ordinal++)
        {
            if (string.Equals(_names[ordinal], name, StringComparison.Ordinal))
            {
                return ordinal;
            }
        }

        throw new IndexOutOfRangeException($"Column '{name}' was not found.");
    }

    public override string GetDataTypeName(int ordinal)
    {
        EnsureOrdinal(ordinal);
        return _types[ordinal].Name;
    }

    public override Type GetFieldType(int ordinal)
    {
        EnsureOrdinal(ordinal);
        return _types[ordinal];
    }

    public override bool IsDBNull(int ordinal)
    {
        return GetValue(ordinal) is DBNull;
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
        return (bool)GetTypedValue(ordinal);
    }

    public override byte GetByte(int ordinal)
    {
        return Convert.ToByte(GetTypedValue(ordinal), CultureInfo.InvariantCulture);
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
        return Convert.ToDateTime(GetTypedValue(ordinal), CultureInfo.InvariantCulture);
    }

    public override decimal GetDecimal(int ordinal)
    {
        return Convert.ToDecimal(GetTypedValue(ordinal), CultureInfo.InvariantCulture);
    }

    public override double GetDouble(int ordinal)
    {
        return Convert.ToDouble(GetTypedValue(ordinal), CultureInfo.InvariantCulture);
    }

    public override float GetFloat(int ordinal)
    {
        return Convert.ToSingle(GetTypedValue(ordinal), CultureInfo.InvariantCulture);
    }

    public override Guid GetGuid(int ordinal)
    {
        throw new NotSupportedException();
    }

    public override short GetInt16(int ordinal)
    {
        return Convert.ToInt16(GetTypedValue(ordinal), CultureInfo.InvariantCulture);
    }

    public override int GetInt32(int ordinal)
    {
        return Convert.ToInt32(GetTypedValue(ordinal), CultureInfo.InvariantCulture);
    }

    public override long GetInt64(int ordinal)
    {
        return Convert.ToInt64(GetTypedValue(ordinal), CultureInfo.InvariantCulture);
    }

    public override string GetString(int ordinal)
    {
        return Convert.ToString(GetTypedValue(ordinal), CultureInfo.InvariantCulture)
               ?? string.Empty;
    }

    public override DataTable GetSchemaTable()
    {
        var table = new DataTable("SchemaTable");
        table.Columns.Add(SchemaTableColumn.ColumnName, typeof(string));
        table.Columns.Add(SchemaTableColumn.ColumnOrdinal, typeof(int));
        table.Columns.Add(SchemaTableColumn.DataType, typeof(Type));
        table.Columns.Add(SchemaTableColumn.ProviderType, typeof(int));
        table.Columns.Add(SchemaTableColumn.AllowDBNull, typeof(bool));

        for (var ordinal = 0; ordinal < FieldCount; ordinal++)
        {
            var row = table.NewRow();
            row[SchemaTableColumn.ColumnName] = _names[ordinal];
            row[SchemaTableColumn.ColumnOrdinal] = ordinal;
            row[SchemaTableColumn.DataType] = _types[ordinal];
            row[SchemaTableColumn.ProviderType] = 0;
            row[SchemaTableColumn.AllowDBNull] = _nullable[ordinal];
            table.Rows.Add(row);
        }

        return table;
    }

    public override void Close()
    {
        _isClosed = true;
        _rowIndex = _rows.Length;
    }

    private object GetTypedValue(int ordinal)
    {
        var value = GetValue(ordinal);
        if (value is DBNull)
        {
            throw new InvalidCastException($"Column '{GetName(ordinal)}' is null.");
        }

        return value;
    }

    private void EnsureReadableRow()
    {
        if (_rowIndex < 0 || _rowIndex >= _rows.Length)
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

    private static bool[] InferNullability(InlineData data)
    {
        var result = new bool[data.Columns.Count];
        foreach (var row in data.Rows)
        {
            for (var ordinal = 0; ordinal < row.Values.Count; ordinal++)
            {
                result[ordinal] |= row.Values[ordinal] is NullLiteral;
            }
        }

        return result;
    }

    private static Type[] InferTypes(InlineData data)
    {
        // Inline хранит только literal-ы, поэтому самый широкий тип колонки можно вывести один раз.
        var kinds = new InlineColumnKind[data.Columns.Count];
        foreach (var row in data.Rows)
        {
            for (var ordinal = 0; ordinal < row.Values.Count; ordinal++)
            {
                kinds[ordinal] = Merge(kinds[ordinal], Kind(row.Values[ordinal]));
            }
        }

        return kinds.Select(static kind => kind switch
        {
            InlineColumnKind.Integer => typeof(long),
            InlineColumnKind.Number => typeof(double),
            InlineColumnKind.Boolean => typeof(bool),
            _ => typeof(string)
        }).ToArray();
    }

    private static object? ConvertLiteral(Literal literal, Type targetType)
    {
        if (literal is NullLiteral)
        {
            return null;
        }

        if (targetType == typeof(string))
        {
            return literal switch
            {
                StringLiteral value => value.Value,
                IntegerLiteral value => value.Value.ToString(CultureInfo.InvariantCulture),
                NumberLiteral value => value.Value.ToString("0.0###############", CultureInfo.InvariantCulture),
                BooleanLiteral value => value.Value ? "true" : "false",
                _ => literal.ToString()
            };
        }

        if (targetType == typeof(double))
        {
            return literal switch
            {
                IntegerLiteral value => (double)value.Value,
                NumberLiteral value => value.Value,
                _ => throw new InvalidOperationException($"Inline literal '{literal}' cannot be converted to Number.")
            };
        }

        if (targetType == typeof(long) && literal is IntegerLiteral integer)
        {
            return integer.Value;
        }

        if (targetType == typeof(bool) && literal is BooleanLiteral boolean)
        {
            return boolean.Value;
        }

        throw new InvalidOperationException($"Inline literal '{literal}' cannot be converted to {targetType.Name}.");
    }

    private static InlineColumnKind Kind(Literal literal)
    {
        return literal switch
        {
            NullLiteral => InlineColumnKind.Unknown,
            IntegerLiteral => InlineColumnKind.Integer,
            NumberLiteral => InlineColumnKind.Number,
            BooleanLiteral => InlineColumnKind.Boolean,
            StringLiteral => InlineColumnKind.Text,
            _ => InlineColumnKind.Text
        };
    }

    private static InlineColumnKind Merge(InlineColumnKind current, InlineColumnKind next)
    {
        if (next == InlineColumnKind.Unknown)
        {
            return current;
        }

        if (current == InlineColumnKind.Unknown)
        {
            return next;
        }

        if (current == next)
        {
            return current;
        }

        if ((current == InlineColumnKind.Integer && next == InlineColumnKind.Number) ||
            (current == InlineColumnKind.Number && next == InlineColumnKind.Integer))
        {
            return InlineColumnKind.Number;
        }

        return InlineColumnKind.Text;
    }

    private enum InlineColumnKind
    {
        Unknown,
        Integer,
        Number,
        Boolean,
        Text
    }
}

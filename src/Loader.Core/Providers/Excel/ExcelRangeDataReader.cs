using System.Collections;
using System.Data;
using System.Data.Common;
using System.Globalization;
using Loader.Core.Decorators;
using Sylvan.Data.Excel;

namespace Loader.Core.Providers.Excel;

/// <summary>
/// Reader-wrapper, который превращает прямоугольный Excel range в самостоятельную таблицу.
/// Заголовок range читается вручную, поэтому диапазон может начинаться не с первой строки листа.
/// </summary>
internal sealed class ExcelRangeDataReader : DbDataReaderDecorator
{
    private readonly ExcelCellRange _range;
    private readonly string[] _names;
    private int _currentRow;
    private bool _hasRow;

    private ExcelRangeDataReader(
        DbDataReader inner,
        ExcelCellRange range,
        string[] names,
        int currentRow)
        : base(inner)
    {
        _range = range;
        _names = names;
        _currentRow = currentRow;
    }

    public override int FieldCount => _names.Length;

    public static async ValueTask<DbDataReader> CreateAsync(
        DbDataReader inner,
        ExcelCellRange range,
        bool hasHeader,
        CancellationToken cancellationToken)
    {
        if (!hasHeader)
        {
            return new ExcelRangeDataReader(inner, range, CreateGeneratedNames(range), 0);
        }

        var currentRow = 0;
        while (true)
        {
            if (!await inner.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return new ExcelRangeDataReader(inner, range, CreateGeneratedNames(range), currentRow);
            }

            currentRow = CurrentRowNumber(inner, currentRow);
            if (currentRow >= range.StartRow)
            {
                break;
            }
        }

        var names = Enumerable
            .Range(0, range.ColumnCount)
            .Select(ordinal => HeaderName(inner, range, ordinal))
            .ToArray();
        return new ExcelRangeDataReader(inner, range, names, currentRow);
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

    public override Type GetFieldType(int ordinal)
    {
        EnsureOrdinal(ordinal);
        return typeof(string);
    }

    public override string GetDataTypeName(int ordinal)
    {
        EnsureOrdinal(ordinal);
        return "String";
    }

    public override bool Read()
    {
        while (true)
        {
            if (_range.EndRow is not null && _currentRow >= _range.EndRow.Value)
            {
                _hasRow = false;
                return false;
            }

            if (!Inner.Read())
            {
                _hasRow = false;
                return false;
            }

            _currentRow = CurrentRowNumber(Inner, _currentRow);
            if (_currentRow < _range.StartRow)
            {
                continue;
            }

            if (_range.EndRow is not null && _currentRow > _range.EndRow.Value)
            {
                _hasRow = false;
                return false;
            }

            _hasRow = true;
            return true;
        }
    }

    public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            if (_range.EndRow is not null && _currentRow >= _range.EndRow.Value)
            {
                _hasRow = false;
                return false;
            }

            if (!await Inner.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                _hasRow = false;
                return false;
            }

            _currentRow = CurrentRowNumber(Inner, _currentRow);
            if (_currentRow < _range.StartRow)
            {
                continue;
            }

            if (_range.EndRow is not null && _currentRow > _range.EndRow.Value)
            {
                _hasRow = false;
                return false;
            }

            _hasRow = true;
            return true;
        }
    }

    public override bool IsDBNull(int ordinal)
    {
        EnsureReadableRow();
        var physicalOrdinal = PhysicalOrdinal(ordinal);
        return IsMissingCell(physicalOrdinal) || Inner.IsDBNull(physicalOrdinal);
    }

    public override object GetValue(int ordinal)
    {
        EnsureReadableRow();
        var physicalOrdinal = PhysicalOrdinal(ordinal);
        if (IsMissingCell(physicalOrdinal) || Inner.IsDBNull(physicalOrdinal))
        {
            return DBNull.Value;
        }

        return Convert.ToString(Inner.GetValue(physicalOrdinal), CultureInfo.InvariantCulture) ?? string.Empty;
    }

    public override int GetValues(object[] values)
    {
        var count = Math.Min(values.Length, FieldCount);
        for (var ordinal = 0; ordinal < count; ordinal++)
        {
            values[ordinal] = GetValue(ordinal);
        }

        return count;
    }

    public override string GetString(int ordinal)
    {
        var value = GetValue(ordinal);
        if (value == DBNull.Value)
        {
            throw new InvalidCastException($"Column '{GetName(ordinal)}' is null.");
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
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
        return Convert.ToBoolean(GetTypedValue(ordinal), CultureInfo.InvariantCulture);
    }

    public override byte GetByte(int ordinal)
    {
        return Convert.ToByte(GetTypedValue(ordinal), CultureInfo.InvariantCulture);
    }

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(GetString(ordinal));
        if (buffer is null)
        {
            return bytes.Length;
        }

        var available = Math.Max(0, bytes.Length - checked((int)dataOffset));
        var count = Math.Min(length, available);
        Array.Copy(bytes, checked((int)dataOffset), buffer, bufferOffset, count);
        return count;
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
        return Guid.Parse(GetString(ordinal));
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
            row[SchemaTableColumn.DataType] = typeof(string);
            row[SchemaTableColumn.ProviderType] = 0;
            row[SchemaTableColumn.AllowDBNull] = true;
            table.Rows.Add(row);
        }

        return table;
    }

    private static string[] CreateGeneratedNames(ExcelCellRange range)
    {
        return Enumerable
            .Range(range.StartColumn - 1, range.ColumnCount)
            .Select(ExcelCellRange.GetColumnName)
            .ToArray();
    }

    private static string HeaderName(DbDataReader inner, ExcelCellRange range, int ordinal)
    {
        var physicalOrdinal = range.StartColumn - 1 + ordinal;
        if (IsMissingCell(inner, physicalOrdinal) || inner.IsDBNull(physicalOrdinal))
        {
            return ExcelCellRange.GetColumnName(physicalOrdinal);
        }

        var value = Convert.ToString(inner.GetValue(physicalOrdinal), CultureInfo.InvariantCulture);
        return string.IsNullOrEmpty(value)
            ? ExcelCellRange.GetColumnName(physicalOrdinal)
            : value;
    }

    private int PhysicalOrdinal(int ordinal)
    {
        EnsureOrdinal(ordinal);
        return _range.StartColumn - 1 + ordinal;
    }

    private object GetTypedValue(int ordinal)
    {
        var value = GetValue(ordinal);
        if (value == DBNull.Value)
        {
            throw new InvalidCastException($"Column '{GetName(ordinal)}' is null.");
        }

        return value;
    }

    private bool IsMissingCell(int physicalOrdinal)
    {
        return IsMissingCell(Inner, physicalOrdinal);
    }

    private static bool IsMissingCell(DbDataReader reader, int physicalOrdinal)
    {
        return reader is ExcelDataReader excelReader
            ? physicalOrdinal >= excelReader.RowFieldCount
            : physicalOrdinal >= reader.FieldCount;
    }

    private static int CurrentRowNumber(DbDataReader reader, int previousRow)
    {
        return reader is ExcelDataReader excelReader
            ? excelReader.RowNumber
            : previousRow + 1;
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

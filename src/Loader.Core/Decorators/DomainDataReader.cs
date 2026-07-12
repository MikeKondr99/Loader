using System.Data.Common;
using System.Collections;
using System.Data;

namespace Loader.Core.Decorators;

/// <summary>
/// Base reader for domain-level streams. It exposes normalized schema and guarantees
/// that value access is valid only while the reader is positioned on a readable row.
/// </summary>
public abstract class DomainDataReader : DbDataReaderDecorator
{
    protected DomainDataReader(DbDataReader inner)
        : base(inner)
    {
    }

    public abstract DataSchema DataSchema { get; }

    protected bool HasReadableRow { get; set; }

    public override int FieldCount => DataSchema.Fields.Count;

    public override Type GetFieldType(int ordinal) => DataSchema.GetField(ordinal).ClrType;

    public override string GetName(int ordinal) => DataSchema.GetField(ordinal).Name;

    public override int GetOrdinal(string name) => DataSchema.GetOrdinal(name);

    public override bool IsDBNull(int ordinal)
    {
        return GetValue(ordinal) == DBNull.Value;
    }

    public override bool GetBoolean(int ordinal) => GetTypedValue<bool>(ordinal, nameof(GetBoolean));

    public override byte GetByte(int ordinal) => GetTypedValue<byte>(ordinal, nameof(GetByte));

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
    {
        EnsureFieldClrType(ordinal, typeof(byte[]), nameof(GetBytes));
        var bytes = GetTypedValue<byte[]>(ordinal, nameof(GetBytes));
        if (buffer is null)
        {
            return bytes.Length;
        }

        var available = Math.Max(0, bytes.Length - checked((int)dataOffset));
        var count = Math.Min(length, available);
        Array.Copy(bytes, dataOffset, buffer, bufferOffset, count);
        return count;
    }

    public override char GetChar(int ordinal) => GetTypedValue<char>(ordinal, nameof(GetChar));

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

    public override DateTime GetDateTime(int ordinal) => GetTypedValue<DateTime>(ordinal, nameof(GetDateTime));

    public override decimal GetDecimal(int ordinal) => GetTypedValue<decimal>(ordinal, nameof(GetDecimal));

    public override double GetDouble(int ordinal) => GetTypedValue<double>(ordinal, nameof(GetDouble));

    public override T GetFieldValue<T>(int ordinal)
    {
        var expectedType = typeof(T);
        if (expectedType != typeof(object))
        {
            EnsureFieldClrType(ordinal, expectedType, $"{nameof(GetFieldValue)}<{expectedType.Name}>");
        }

        var value = GetValue(ordinal);
        if (value == DBNull.Value)
        {
            if (expectedType == typeof(object) || expectedType == typeof(DBNull))
            {
                return (T)value;
            }

            throw CreateDbNullCastException(ordinal);
        }

        if (value is T typedValue)
        {
            return typedValue;
        }

        throw CreateValueTypeMismatchException(ordinal, expectedType, value.GetType());
    }

    public override float GetFloat(int ordinal) => GetTypedValue<float>(ordinal, nameof(GetFloat));

    public override Guid GetGuid(int ordinal) => GetTypedValue<Guid>(ordinal, nameof(GetGuid));

    public override short GetInt16(int ordinal) => GetTypedValue<short>(ordinal, nameof(GetInt16));

    public override int GetInt32(int ordinal) => GetTypedValue<int>(ordinal, nameof(GetInt32));

    public override long GetInt64(int ordinal) => GetTypedValue<long>(ordinal, nameof(GetInt64));

    public override string GetString(int ordinal) => GetTypedValue<string>(ordinal, nameof(GetString));

    public override DataTable GetSchemaTable()
    {
        var table = new DataTable("SchemaTable");
        table.Columns.Add(SchemaTableColumn.ColumnName, typeof(string));
        table.Columns.Add(SchemaTableColumn.ColumnOrdinal, typeof(int));
        table.Columns.Add(SchemaTableColumn.DataType, typeof(Type));
        table.Columns.Add(SchemaTableColumn.ProviderType, typeof(int));
        table.Columns.Add(SchemaTableColumn.ColumnSize, typeof(int));
        table.Columns.Add(SchemaTableColumn.NumericPrecision, typeof(int));
        table.Columns.Add(SchemaTableColumn.NumericScale, typeof(int));
        table.Columns.Add(SchemaTableColumn.AllowDBNull, typeof(bool));
        table.Columns.Add(SchemaTableColumn.IsKey, typeof(bool));
        table.Columns.Add(SchemaTableColumn.IsUnique, typeof(bool));
        table.Columns.Add(SchemaTableColumn.IsLong, typeof(bool));

        foreach (var field in DataSchema.Fields)
        {
            var row = table.NewRow();
            row[SchemaTableColumn.ColumnName] = field.Name;
            row[SchemaTableColumn.ColumnOrdinal] = field.Ordinal;
            row[SchemaTableColumn.DataType] = field.ClrType;
            row[SchemaTableColumn.ProviderType] = field.Ordinal;
            row[SchemaTableColumn.ColumnSize] = ToDbValue(field.ColumnSize);
            row[SchemaTableColumn.NumericPrecision] = ToDbValue(field.NumericPrecision);
            row[SchemaTableColumn.NumericScale] = ToDbValue(field.NumericScale);
            row[SchemaTableColumn.AllowDBNull] = field.AllowDBNull ?? true;
            row[SchemaTableColumn.IsKey] = false;
            row[SchemaTableColumn.IsUnique] = false;
            row[SchemaTableColumn.IsLong] = false;
            table.Rows.Add(row);
        }

        return table;
    }

    private static object ToDbValue(int? value)
    {
        return value.HasValue ? value.Value : DBNull.Value;
    }

    public override IEnumerator GetEnumerator()
    {
        throw new NotSupportedException("DomainDataReader supports only explicit Read() and accessor methods.");
    }

    protected void EnsureReadableRow()
    {
        if (!HasReadableRow)
        {
            throw new InvalidOperationException("Reader is not positioned on a row.");
        }
    }

    protected object GetRequiredValue(int ordinal)
    {
        var value = GetValue(ordinal);
        return value == DBNull.Value
            ? throw CreateDbNullCastException(ordinal)
            : value;
    }

    protected T GetTypedValue<T>(int ordinal, string accessorName)
    {
        EnsureFieldClrType(ordinal, typeof(T), accessorName);
        var value = GetRequiredValue(ordinal);
        return value is T typedValue
            ? typedValue
            : throw CreateValueTypeMismatchException(ordinal, typeof(T), value.GetType());
    }

    protected DataField EnsureFieldClrType(int ordinal, Type accessorType, string accessorName)
    {
        var field = DataSchema.GetField(ordinal);
        if (field.ClrType == accessorType)
        {
            return field;
        }

        throw new InvalidCastException(
            $"Column '{field.Name}' at ordinal {ordinal} has CLR type '{field.ClrType.FullName}' and cannot be read with accessor '{accessorName}'.");
    }

    protected static InvalidCastException CreateDbNullCastException(int ordinal)
    {
        return new InvalidCastException($"Column ordinal {ordinal} contains DBNull.");
    }

    protected static InvalidCastException CreateValueTypeMismatchException(int ordinal, Type expectedType, Type actualType)
    {
        return new InvalidCastException(
            $"Column ordinal {ordinal} value has CLR type '{actualType.FullName}', but schema requires '{expectedType.FullName}'.");
    }
}

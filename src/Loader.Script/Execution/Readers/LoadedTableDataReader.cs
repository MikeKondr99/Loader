using System.Data.Common;
using System.Globalization;
using Loader.Core.Decorators;
using Loader.Core.Models;

namespace Loader.Script.Execution;

/// <summary>
/// Накладывает логическую схему LoadedTable поверх физического reader-а из DWH.
/// Например Time физически хранится в ClickHouse как DateTime, но следующий LOAD должен видеть Time.
/// </summary>
internal sealed class LoadedTableDataReader : DbDataReaderDecorator
{
    private readonly IReadOnlyList<LoadedTableField> fields;

    public LoadedTableDataReader(DbDataReader inner, IReadOnlyList<LoadedTableField> fields)
        : base(inner)
    {
        if (fields.Count != inner.FieldCount)
        {
            throw new ArgumentException(
                $"Loaded table field count {fields.Count} does not match reader field count {inner.FieldCount}.",
                nameof(fields));
        }

        this.fields = fields;
    }

    public override Type GetFieldType(int ordinal)
    {
        EnsureOrdinal(ordinal);
        return fields[ordinal].DataType == DataType.Time
            ? typeof(TimeOnly)
            : Inner.GetFieldType(ordinal);
    }

    public override string GetDataTypeName(int ordinal)
    {
        EnsureOrdinal(ordinal);
        return fields[ordinal].DataType == DataType.Time
            ? nameof(DataType.Time)
            : Inner.GetDataTypeName(ordinal);
    }

    public override object GetValue(int ordinal)
    {
        EnsureOrdinal(ordinal);
        var value = Inner.GetValue(ordinal);
        if (value is null || value == DBNull.Value || fields[ordinal].DataType != DataType.Time)
        {
            return value ?? DBNull.Value;
        }

        return ToTimeOnly(value);
    }

    public override T GetFieldValue<T>(int ordinal)
    {
        var value = GetValue(ordinal);
        if (value is DBNull && typeof(T) == typeof(DBNull))
        {
            return (T)value;
        }

        return value is T typedValue
            ? typedValue
            : throw new InvalidCastException($"Column ordinal {ordinal} value has CLR type '{value.GetType().FullName}', but {typeof(T).FullName} was expected.");
    }

    public override bool IsDBNull(int ordinal)
    {
        EnsureOrdinal(ordinal);
        return Inner.IsDBNull(ordinal);
    }

    private static object ToTimeOnly(object value)
    {
        return value switch
        {
            TimeOnly time => time,
            TimeSpan timeSpan => TimeOnly.FromTimeSpan(timeSpan),
            DateTime dateTime => TimeOnly.FromDateTime(dateTime),
            string text => TimeOnly.Parse(text, CultureInfo.InvariantCulture),
            _ => value
        };
    }

    private void EnsureOrdinal(int ordinal)
    {
        if (ordinal < 0 || ordinal >= fields.Count)
        {
            throw new IndexOutOfRangeException($"Column ordinal {ordinal} is out of range.");
        }
    }
}

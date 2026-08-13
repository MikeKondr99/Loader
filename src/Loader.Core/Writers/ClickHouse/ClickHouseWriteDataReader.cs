using System.Data.Common;
using System.Globalization;
using Loader.Core.Decorators;
using Loader.Core.Models;

namespace Loader.Core.Writers.ClickHouse;

/// <summary>
/// Адаптирует доменный reader под физические типы ClickHouse перед bulk insert.
/// Например доменный Time пишется в ClickHouse как DateTime с датой-якорем 1970-01-01.
/// </summary>
internal sealed class ClickHouseWriteDataReader : DbDataReaderDecorator
{
    private readonly DataSchema schema;
    private readonly bool hasTimeFields;

    private ClickHouseWriteDataReader(DomainDataReader inner)
        : base(inner)
    {
        schema = inner.DataSchema;
        hasTimeFields = schema.Fields.Any(static field => field.DataType == DataType.Time);
    }

    public static DbDataReader Wrap(DomainDataReader reader)
    {
        return reader.DataSchema.Fields.Any(static field => field.DataType == DataType.Time)
            ? new ClickHouseWriteDataReader(reader)
            : reader;
    }

    public override object GetValue(int ordinal)
    {
        var value = Inner.GetValue(ordinal);
        if (value is null || value == DBNull.Value || schema.GetField(ordinal).DataType != DataType.Time)
        {
            return value ?? DBNull.Value;
        }

        return TimeToDateTime(value);
    }

    public override Type GetFieldType(int ordinal)
    {
        return schema.GetField(ordinal).DataType == DataType.Time
            ? typeof(DateTime)
            : Inner.GetFieldType(ordinal);
    }

    public override string GetDataTypeName(int ordinal)
    {
        return schema.GetField(ordinal).DataType == DataType.Time
            ? "DateTime"
            : Inner.GetDataTypeName(ordinal);
    }

    public override DateTime GetDateTime(int ordinal)
    {
        var value = GetValue(ordinal);
        return value is DateTime dateTime
            ? dateTime
            : throw new InvalidCastException($"Column ordinal {ordinal} value has CLR type '{value.GetType().FullName}', but DateTime was expected.");
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

    public override int GetValues(object[] values)
    {
        var count = Inner.GetValues(values);
        if (!hasTimeFields)
        {
            return count;
        }

        for (var ordinal = 0; ordinal < count; ordinal++)
        {
            if (values[ordinal] is null || values[ordinal] == DBNull.Value ||
                schema.GetField(ordinal).DataType != DataType.Time)
            {
                continue;
            }

            values[ordinal] = TimeToDateTime(values[ordinal]);
        }

        return count;
    }

    private static DateTime TimeToDateTime(object value)
    {
        return value switch
        {
            TimeOnly time => TimeToDateTime(time),
            TimeSpan timeSpan => TimeToDateTime(TimeOnly.FromTimeSpan(timeSpan)),
            DateTime dateTime => TimeToDateTime(TimeOnly.FromDateTime(dateTime)),
            string text => TimeToDateTime(TimeOnly.Parse(text, CultureInfo.InvariantCulture)),
            _ => TimeToDateTime(TimeOnly.FromDateTime(Convert.ToDateTime(value, CultureInfo.InvariantCulture)))
        };
    }

    private static DateTime TimeToDateTime(TimeOnly time)
    {
        return new DateTime(1970, 1, 1, time.Hour, time.Minute, time.Second, time.Millisecond);
    }
}

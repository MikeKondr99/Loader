using System.Data.Common;

namespace Loader.Core.Decorators;

/// <summary>
/// Domain reader that normalizes a raw DbDataReader: builds domain schema and converts provider values on demand.
/// </summary>
internal sealed class NormalizingDomainDataReader : DomainDataReader
{
    private readonly DataSchema _schema;

    public NormalizingDomainDataReader(DbDataReader inner)
        : base(inner)
    {
        _schema = DataSchema.FromReader(inner);
    }

    public override DataSchema DataSchema => _schema;

    public override bool Read()
    {
        if (!Inner.Read())
        {
            HasReadableRow = false;
            return false;
        }

        HasReadableRow = true;
        return true;
    }

    public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        if (!await Inner.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            HasReadableRow = false;
            return false;
        }

        HasReadableRow = true;
        return true;
    }

    public override object GetValue(int ordinal)
    {
        EnsureReadableRow();
        return ReadAndConvertValue(ordinal);
    }

    public override bool IsDBNull(int ordinal)
    {
        EnsureReadableRow();
        var field = _schema.GetField(ordinal);

        if (!field.ReadValue)
        {
            return true;
        }

        return field.Convert is null
            ? Inner.IsDBNull(ordinal)
            : GetValue(ordinal) == DBNull.Value;
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

    public override bool GetBoolean(int ordinal)
    {
        EnsureReadableRow();
        var field = EnsureFieldClrType(ordinal, typeof(bool), nameof(GetBoolean));
        return ReadTypedValue(ordinal, field, static (reader, index) => reader.GetBoolean(index));
    }

    public override byte GetByte(int ordinal)
    {
        EnsureReadableRow();
        var field = EnsureFieldClrType(ordinal, typeof(byte), nameof(GetByte));
        return ReadTypedValue(ordinal, field, static (reader, index) => reader.GetByte(index));
    }

    public override char GetChar(int ordinal)
    {
        EnsureReadableRow();
        var field = EnsureFieldClrType(ordinal, typeof(char), nameof(GetChar));
        return ReadTypedValue(ordinal, field, static (reader, index) => reader.GetChar(index));
    }

    public override DateTime GetDateTime(int ordinal)
    {
        EnsureReadableRow();
        var field = EnsureFieldClrType(ordinal, typeof(DateTime), nameof(GetDateTime));
        return ReadTypedValue(ordinal, field, static (reader, index) => reader.GetDateTime(index));
    }

    public override decimal GetDecimal(int ordinal)
    {
        EnsureReadableRow();
        var field = EnsureFieldClrType(ordinal, typeof(decimal), nameof(GetDecimal));
        return ReadTypedValue(ordinal, field, static (reader, index) => reader.GetDecimal(index));
    }

    public override double GetDouble(int ordinal)
    {
        EnsureReadableRow();
        var field = EnsureFieldClrType(ordinal, typeof(double), nameof(GetDouble));
        return ReadTypedValue(ordinal, field, static (reader, index) => reader.GetDouble(index));
    }

    public override float GetFloat(int ordinal)
    {
        EnsureReadableRow();
        var field = EnsureFieldClrType(ordinal, typeof(float), nameof(GetFloat));
        return ReadTypedValue(ordinal, field, static (reader, index) => reader.GetFloat(index));
    }

    public override Guid GetGuid(int ordinal)
    {
        EnsureReadableRow();
        var field = EnsureFieldClrType(ordinal, typeof(Guid), nameof(GetGuid));
        return ReadTypedValue(ordinal, field, static (reader, index) => reader.GetGuid(index));
    }

    public override short GetInt16(int ordinal)
    {
        EnsureReadableRow();
        var field = EnsureFieldClrType(ordinal, typeof(short), nameof(GetInt16));
        return ReadTypedValue(ordinal, field, static (reader, index) => reader.GetInt16(index));
    }

    public override int GetInt32(int ordinal)
    {
        EnsureReadableRow();
        var field = EnsureFieldClrType(ordinal, typeof(int), nameof(GetInt32));
        return ReadTypedValue(ordinal, field, static (reader, index) => reader.GetInt32(index));
    }

    public override long GetInt64(int ordinal)
    {
        EnsureReadableRow();
        var field = EnsureFieldClrType(ordinal, typeof(long), nameof(GetInt64));
        return ReadTypedValue(ordinal, field, static (reader, index) => reader.GetInt64(index));
    }

    public override string GetString(int ordinal)
    {
        EnsureReadableRow();
        var field = EnsureFieldClrType(ordinal, typeof(string), nameof(GetString));
        return ReadTypedValue(ordinal, field, static (reader, index) => reader.GetString(index));
    }

    public override T GetFieldValue<T>(int ordinal)
    {
        EnsureReadableRow();
        var expectedType = typeof(T);
        if (expectedType == typeof(object))
        {
            return (T)GetValue(ordinal);
        }

        var field = EnsureFieldClrType(ordinal, expectedType, $"{nameof(GetFieldValue)}<{expectedType.Name}>");
        return ReadTypedValue(ordinal, field, static (reader, index) => reader.GetFieldValue<T>(index));
    }

    private object ReadAndConvertValue(int ordinal)
    {
        var field = _schema.GetField(ordinal);

        try
        {
            if (!field.ReadValue)
            {
                return DBNull.Value;
            }

            var value = Inner.GetValue(ordinal);
            if (value is null || value == DBNull.Value)
            {
                return DBNull.Value;
            }

            return field.Convert is null ? value : field.Convert(value);
        }
        catch (DataReaderValueException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DataReaderValueException(field.Name, ordinal, ex);
        }
    }

    private T ReadTypedValue<T>(int ordinal, DataField field, Func<DbDataReader, int, T> readInner)
    {
        try
        {
            if (!field.ReadValue)
            {
                throw CreateDbNullCastException(ordinal);
            }

            if (field.Convert is null)
            {
                if (Inner.IsDBNull(ordinal))
                {
                    throw CreateDbNullCastException(ordinal);
                }

                return readInner(Inner, ordinal);
            }

            var value = ReadAndConvertValue(ordinal);
            if (value == DBNull.Value)
            {
                throw CreateDbNullCastException(ordinal);
            }

            return value is T typedValue
                ? typedValue
                : throw CreateValueTypeMismatchException(ordinal, typeof(T), value.GetType());
        }
        catch (InvalidCastException)
        {
            throw;
        }
        catch (DataReaderValueException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DataReaderValueException(field.Name, ordinal, ex);
        }
    }
}

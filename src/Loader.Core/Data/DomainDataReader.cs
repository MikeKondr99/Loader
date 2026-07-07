using System.Data.Common;

namespace Loader.Core.Data;

/// <summary>
/// DbDataReader-обертка доменного уровня: читает поток построчно, нормализует значения и хранит snapshot текущей строки.
/// </summary>
public class DomainDataReader : DbDataReaderDecorator
{
    private readonly DataSchema _schema;
    private object[] _rowBuffer = [];
    private bool _hasCurrentRow;

    public DomainDataReader(DbDataReader inner)
        : base(inner)
    {
        _schema = DataSchema.FromReader(inner);
    }

    public DataSchema DataSchema => _schema;

    public override int FieldCount => _schema.Fields.Count;

    public override Type GetFieldType(int ordinal) => DataTypeMapper.ToClrType(_schema.GetField(ordinal).DataType);

    public override string GetName(int ordinal) => _schema.GetField(ordinal).Name;

    public override int GetOrdinal(string name) => _schema.GetOrdinal(name);

    public override bool Read()
    {
        if (!Inner.Read())
        {
            _hasCurrentRow = false;
            return false;
        }

        BufferCurrentRow();
        return true;
    }

    public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        if (!await Inner.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            _hasCurrentRow = false;
            return false;
        }

        BufferCurrentRow();
        return true;
    }

    public override object GetValue(int ordinal)
    {
        EnsureCurrentRow();
        _schema.GetField(ordinal);
        return _rowBuffer[ordinal];
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

    public override bool IsDBNull(int ordinal)
    {
        return GetValue(ordinal) == DBNull.Value;
    }

    private void BufferCurrentRow()
    {
        // 1. Читаем inner reader слева направо, чтобы не нарушать SequentialAccess.
        if (_rowBuffer.Length != FieldCount)
        {
            _rowBuffer = new object[FieldCount];
        }

        // 2. Нормализуем значения один раз и держим snapshot только текущей строки.
        for (var ordinal = 0; ordinal < FieldCount; ordinal++)
        {
            _rowBuffer[ordinal] = ReadAndConvertValue(ordinal);
        }

        _hasCurrentRow = true;
    }

    private object ReadAndConvertValue(int ordinal)
    {
        var field = _schema.GetField(ordinal);

        try
        {
            if (Inner.IsDBNull(ordinal))
            {
                return DBNull.Value;
            }

            var value = Inner.GetValue(ordinal);
            if (value is null)
            {
                return DBNull.Value;
            }

            return DataValueConverter.FromDataType(field.DataType).Convert(value);
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

    private void EnsureCurrentRow()
    {
        if (!_hasCurrentRow)
        {
            throw new InvalidOperationException("Reader is not positioned on a row.");
        }
    }
}


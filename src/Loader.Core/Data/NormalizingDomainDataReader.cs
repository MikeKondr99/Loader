using System.Data.Common;

namespace Loader.Core.Data;

/// <summary>
/// Domain reader that normalizes a raw DbDataReader: builds domain schema,
/// converts provider values, and buffers one current row.
/// </summary>
internal sealed class NormalizingDomainDataReader : DomainDataReader
{
    private readonly DataSchema _schema;
    private object[] _rowBuffer = [];

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

        BufferCurrentRow();
        return true;
    }

    public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        if (!await Inner.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            HasReadableRow = false;
            return false;
        }

        BufferCurrentRow();
        return true;
    }

    public override object GetValue(int ordinal)
    {
        EnsureReadableRow();
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

    private void BufferCurrentRow()
    {
        // 1. Read inner reader left-to-right once, preserving SequentialAccess semantics.
        if (_rowBuffer.Length != FieldCount)
        {
            _rowBuffer = new object[FieldCount];
        }

        // 2. Normalize values once and keep a snapshot of only the current row.
        for (var ordinal = 0; ordinal < FieldCount; ordinal++)
        {
            _rowBuffer[ordinal] = ReadAndConvertValue(ordinal);
        }

        HasReadableRow = true;
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
}

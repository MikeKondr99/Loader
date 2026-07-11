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

    public override int GetValues(object[] values)
    {
        var count = Math.Min(values.Length, FieldCount);
        for (var i = 0; i < count; i++)
        {
            values[i] = GetValue(i);
        }

        return count;
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
}

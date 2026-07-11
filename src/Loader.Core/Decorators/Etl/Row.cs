using System.Globalization;

namespace Loader.Core.Decorators;

/// <summary>
/// Фасад доступа к текущей строке reader-а для будущих Where/Select выражений.
/// </summary>
public sealed class Row
{
    private readonly DomainDataReader _reader;

    public Row(DomainDataReader reader)
    {
        _reader = reader;
    }

    public string? Text(string name)
    {
        var ordinal = _reader.GetOrdinal(name);
        return _reader.IsDBNull(ordinal)
            ? null
            : Convert.ToString(_reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    public decimal? Number(string name)
    {
        var ordinal = _reader.GetOrdinal(name);
        if (_reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = _reader.GetValue(ordinal);
        return value switch
        {
            decimal d => d,
            double d => Convert.ToDecimal(d, CultureInfo.InvariantCulture),
            float f => Convert.ToDecimal(f, CultureInfo.InvariantCulture),
            _ => Convert.ToDecimal(value, CultureInfo.InvariantCulture)
        };
    }

    public long? Integer(string name)
    {
        var ordinal = _reader.GetOrdinal(name);
        return _reader.IsDBNull(ordinal)
            ? null
            : Convert.ToInt64(_reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    public bool? Boolean(string name)
    {
        var ordinal = _reader.GetOrdinal(name);
        return _reader.IsDBNull(ordinal)
            ? null
            : Convert.ToBoolean(_reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    public DateTime? DateTime(string name)
    {
        var ordinal = _reader.GetOrdinal(name);
        return _reader.IsDBNull(ordinal)
            ? null
            : Convert.ToDateTime(_reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    public DateOnly? Date(string name)
    {
        var ordinal = _reader.GetOrdinal(name);
        if (_reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = _reader.GetValue(ordinal);
        return value is DateOnly date
            ? date
            : DateOnly.FromDateTime(Convert.ToDateTime(value, CultureInfo.InvariantCulture));
    }

    public TimeOnly? Time(string name)
    {
        var ordinal = _reader.GetOrdinal(name);
        if (_reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = _reader.GetValue(ordinal);
        return value switch
        {
            TimeOnly time => time,
            TimeSpan timeSpan => TimeOnly.FromTimeSpan(timeSpan),
            _ => TimeOnly.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture)
        };
    }
}

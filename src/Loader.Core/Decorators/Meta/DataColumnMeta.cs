using System.Globalization;

namespace Loader.Core.Decorators;

/// <summary>
/// Метаинформация по одному столбцу, собранная во время чтения stream-а.
/// </summary>
public sealed class DataColumnMeta
{
    private readonly HashSet<object> _uniqueValues = [];
    private readonly int? _maxCardinality;
    private long _nonNullCount;

    internal DataColumnMeta(int ordinal, string name, DataType dataType, int? decimalPrecision, int? decimalScale, int? maxCardinality)
    {
        Ordinal = ordinal;
        Name = name;
        DataType = dataType;
        DecimalPrecision = decimalPrecision;
        DecimalScale = decimalScale;
        _maxCardinality = maxCardinality;
    }

    public int Ordinal { get; }

    public string Name { get; }

    public DataType DataType { get; }

    public int UniqueValueCount => _uniqueValues.Count;

    public bool AllValuesUnique { get; private set; } = true;

    public bool CardinalityExceeded { get; private set; }

    public decimal Density { get; private set; }

    public decimal? Min { get; private set; }

    public decimal? Max { get; private set; }

    public int? DecimalPrecision { get; }

    public int? DecimalScale { get; }

    internal void CollectValue(object value, long rowCount)
    {
        CollectCardinality(value);

        if (value != DBNull.Value)
        {
            _nonNullCount++;
            CollectNumericBounds(value);
        }

        Density = rowCount == 0
            ? 0m
            : _nonNullCount / (decimal)rowCount;
    }

    private void CollectCardinality(object value)
    {
        if (CardinalityExceeded)
        {
            AllValuesUnique = false;
            return;
        }

        if (_maxCardinality == 0)
        {
            CardinalityExceeded = true;
            AllValuesUnique = false;
            return;
        }

        if (!_uniqueValues.Add(value))
        {
            AllValuesUnique = false;
            return;
        }

        if (_maxCardinality is not null && _uniqueValues.Count > _maxCardinality.Value)
        {
            CardinalityExceeded = true;
            AllValuesUnique = false;
            _uniqueValues.Clear();
        }
    }

    private void CollectNumericBounds(object value)
    {
        if (DataType is not (DataType.Integer or DataType.Number))
        {
            return;
        }

        var numeric = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        Min = Min is null || numeric < Min.Value ? numeric : Min;
        Max = Max is null || numeric > Max.Value ? numeric : Max;
    }
}

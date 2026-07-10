namespace Loader.Core.Data.AutoCast;

internal sealed class AutoCastDataReader : DomainDataReaderDecorator
{
    private readonly DataSchema _schema;
    private readonly IAutoCastFormat?[] _formatsByOrdinal;
    private object[] _rowBuffer = [];

    public AutoCastDataReader(DomainDataReader inner, AutoCastSchema autoCastSchema)
        : base(inner)
    {
        _formatsByOrdinal = BuildFormats(inner.DataSchema, autoCastSchema);
        _schema = BuildSchema(inner.DataSchema, _formatsByOrdinal);
    }

    public override DataSchema DataSchema => _schema;

    public override bool Read()
    {
        if (!InnerDomain.Read())
        {
            HasReadableRow = false;
            return false;
        }

        BufferCurrentRow();
        return true;
    }

    public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        if (!await InnerDomain.ReadAsync(cancellationToken).ConfigureAwait(false))
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

    private static IAutoCastFormat?[] BuildFormats(DataSchema schema, AutoCastSchema autoCastSchema)
    {
        var formats = new IAutoCastFormat?[schema.Fields.Count];
        foreach (var field in autoCastSchema.Fields)
        {
            var ordinal = schema.GetOrdinal(field.Name);
            if (ShouldAutoCast(schema.GetField(ordinal)))
            {
                formats[ordinal] = field.Format;
            }
        }

        return formats;
    }

    private static DataSchema BuildSchema(DataSchema innerSchema, IAutoCastFormat?[] formatsByOrdinal)
    {
        var fields = innerSchema.Fields
            .Select(field =>
            {
                var format = formatsByOrdinal[field.Ordinal];
                return format is null
                    ? field
                    : field with
                    {
                        DataType = format.DataType,
                        ClrType = format.ClrType,
                        Convert = null,
                        ReadValue = true
                    };
            })
            .ToArray();

        return new DataSchema
        {
            Fields = fields
        };
    }

    private static bool ShouldAutoCast(DataField field)
    {
        return field.DataType == DataType.Text && field.ClrType == typeof(string);
    }

    private void BufferCurrentRow()
    {
        if (_rowBuffer.Length != FieldCount)
        {
            _rowBuffer = new object[FieldCount];
        }

        for (var ordinal = 0; ordinal < FieldCount; ordinal++)
        {
            _rowBuffer[ordinal] = ReadAndConvertValue(ordinal);
        }

        HasReadableRow = true;
    }

    private object ReadAndConvertValue(int ordinal)
    {
        var value = InnerDomain.GetValue(ordinal);
        if (value == DBNull.Value)
        {
            return DBNull.Value;
        }

        var format = _formatsByOrdinal[ordinal];
        if (format is null)
        {
            return value;
        }

        if (format.TryConvert(value, out var converted))
        {
            return converted;
        }

        var field = _schema.GetField(ordinal);
        throw new DataReaderValueException(
            field.Name,
            ordinal,
            new FormatException($"Value '{value}' cannot be converted by auto-cast format '{format.Name}'."));
    }
}

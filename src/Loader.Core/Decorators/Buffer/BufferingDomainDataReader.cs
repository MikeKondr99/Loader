namespace Loader.Core.Decorators;

/// <summary>
/// Декоратор, который материализует текущую строку доменного reader-а в object[].
/// Нужен для SequentialAccess и для доступа к полям текущей строки в любом порядке.
/// </summary>
internal sealed class BufferingDomainDataReader : DomainDataReaderDecorator
{
    private object[] _rowBuffer = [];

    public BufferingDomainDataReader(DomainDataReader inner)
        : base(inner)
    {
    }

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
        DataSchema.GetField(ordinal);
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
        if (_rowBuffer.Length != FieldCount)
        {
            _rowBuffer = new object[FieldCount];
        }

        for (var ordinal = 0; ordinal < FieldCount; ordinal++)
        {
            _rowBuffer[ordinal] = InnerDomain.GetValue(ordinal);
        }

        HasReadableRow = true;
    }
}

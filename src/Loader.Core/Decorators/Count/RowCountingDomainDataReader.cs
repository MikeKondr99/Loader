namespace Loader.Core.Decorators;

/// <summary>
/// Counts rows without collecting column-level meta.
/// </summary>
public sealed class RowCountingDomainDataReader : DomainDataReader
{
    private readonly DomainDataReader _inner;

    public RowCountingDomainDataReader(DomainDataReader inner)
        : base(inner)
    {
        _inner = inner;
    }

    public override DataSchema DataSchema => _inner.DataSchema;

    public long RowCount { get; private set; }

    public override bool Read()
    {
        if (!_inner.Read())
        {
            HasReadableRow = false;
            return false;
        }

        HasReadableRow = true;
        RowCount++;
        return true;
    }

    public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        if (!await _inner.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            HasReadableRow = false;
            return false;
        }

        HasReadableRow = true;
        RowCount++;
        return true;
    }

    public override object GetValue(int ordinal)
    {
        EnsureReadableRow();
        return _inner.GetValue(ordinal);
    }

    public override int GetValues(object[] values)
    {
        EnsureReadableRow();
        return _inner.GetValues(values);
    }
}

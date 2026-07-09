namespace Loader.Core.Data;

/// <summary>
/// Base decorator for domain-level pipeline readers. It reuses normalized schema and
/// values from the inner domain reader, but owns its readable-row state.
/// </summary>
internal abstract class DomainDataReaderDecorator : DomainDataReader
{
    protected DomainDataReaderDecorator(DomainDataReader inner)
        : base(inner)
    {
        InnerDomain = inner;
    }

    protected DomainDataReader InnerDomain { get; }

    public override DataSchema DataSchema => InnerDomain.DataSchema;

    public override object GetValue(int ordinal)
    {
        EnsureReadableRow();
        return InnerDomain.GetValue(ordinal);
    }

    public override int GetValues(object[] values)
    {
        EnsureReadableRow();
        return InnerDomain.GetValues(values);
    }

}

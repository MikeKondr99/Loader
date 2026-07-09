using System.Collections;
using System.Data.Common;

namespace Loader.Core.Data;

/// <summary>
/// Декоратор, который собирает метаинформацию по строкам доменного reader-а.
/// </summary>
internal sealed class MetaCollectingDataReader : DomainDataReaderDecorator
{
    private readonly DataMetaContainer _metaContainer;

    public MetaCollectingDataReader(DomainDataReader inner, DataMetaContainer metaContainer)
        : base(inner)
    {
        _metaContainer = metaContainer;
        _metaContainer.Start(inner.DataSchema, inner.GetColumnSchema());
    }

    public override bool Read()
    {
        try
        {
            if (!Inner.Read())
            {
                HasReadableRow = false;
                _metaContainer.Complete();
                return false;
            }

            HasReadableRow = true;
            _metaContainer.CollectRow(this);
            return true;
        }
        catch
        {
            _metaContainer.Fail();
            throw;
        }
    }

    public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!await Inner.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                HasReadableRow = false;
                _metaContainer.Complete();
                return false;
            }

            HasReadableRow = true;
            _metaContainer.CollectRow(this);
            return true;
        }
        catch
        {
            _metaContainer.Fail();
            throw;
        }
    }

    public override IEnumerator GetEnumerator()
    {
        return new DbEnumerator(this);
    }
}

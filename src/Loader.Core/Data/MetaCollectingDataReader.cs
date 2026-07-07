using System.Collections;
using System.Data.Common;

namespace Loader.Core.Data;

/// <summary>
/// Декоратор, который собирает метаинформацию по строкам доменного reader-а.
/// </summary>
internal sealed class MetaCollectingDataReader : DbDataReaderDecorator
{
    private readonly DomainDataReader _inner;
    private readonly DataMetaContainer _metaContainer;

    public MetaCollectingDataReader(DomainDataReader inner, DataMetaContainer metaContainer)
        : base(inner)
    {
        _inner = inner;
        _metaContainer = metaContainer;
        _metaContainer.Start(inner.DataSchema, inner.GetColumnSchema());
    }

    public override bool Read()
    {
        try
        {
            if (!_inner.Read())
            {
                _metaContainer.Complete();
                return false;
            }

            _metaContainer.CollectRow(_inner);
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
            if (!await _inner.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                _metaContainer.Complete();
                return false;
            }

            _metaContainer.CollectRow(_inner);
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

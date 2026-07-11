using System.Collections;
using System.Data.Common;

namespace Loader.Core.Decorators.Etl;

/// <summary>
/// DbDataReader-декоратор, который останавливает чтение после заданного количества строк.
/// </summary>
internal sealed class LimitDbDataReader : DomainDataReaderDecorator
{
    private readonly int _limit;
    private int _readRows;

    public LimitDbDataReader(DomainDataReader inner, int limit)
        : base(inner)
    {
        if (limit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit must be greater than or equal to zero.");
        }

        _limit = limit;
    }

    public override bool HasRows => _limit > 0 && Inner.HasRows;

    public override bool Read()
    {
        if (_readRows >= _limit)
        {
            HasReadableRow = false;
            return false;
        }

        if (!Inner.Read())
        {
            HasReadableRow = false;
            return false;
        }

        _readRows++;
        HasReadableRow = true;
        return true;
    }

    public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        if (_readRows >= _limit)
        {
            HasReadableRow = false;
            return false;
        }

        if (!await Inner.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            HasReadableRow = false;
            return false;
        }

        _readRows++;
        HasReadableRow = true;
        return true;
    }

    public override IEnumerator GetEnumerator()
    {
        return new DbEnumerator(this);
    }
}

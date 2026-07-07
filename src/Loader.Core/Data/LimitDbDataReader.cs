using System.Collections;
using System.Data.Common;

namespace Loader.Core.Data;

/// <summary>
/// DbDataReader-декоратор, который останавливает чтение после заданного количества строк.
/// </summary>
internal sealed class LimitDbDataReader : DbDataReaderDecorator
{
    private readonly int _limit;
    private int _readRows;

    public LimitDbDataReader(DbDataReader inner, int limit)
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
            return false;
        }

        if (!Inner.Read())
        {
            return false;
        }

        _readRows++;
        return true;
    }

    public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        if (_readRows >= _limit)
        {
            return false;
        }

        if (!await Inner.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        _readRows++;
        return true;
    }

    public override IEnumerator GetEnumerator()
    {
        return new DbEnumerator(this);
    }
}

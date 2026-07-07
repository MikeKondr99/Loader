using System.Data.Common;

namespace Loader.Core.Data;

/// <summary>
/// Точка входа для stream-операций поверх стандартного DbDataReader.
/// </summary>
public static class DbDataReaderExtensions
{

    public static DomainDataReader Normalize(this DbDataReader reader)
    {
        return new DomainDataReader(reader);
    }

    public static DomainDataReader Where(this DomainDataReader reader, Func<Row, bool> predicate)
    {
        // TODO: поменять это дичь должно быть просто Where(reader)
        return new DomainDataReader(new WhereDomainDataReader(reader, predicate));
    }

    public static DomainDataReader Limit(this DomainDataReader reader, int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Limit must be greater than or equal to zero.");
        }

        // TODO: поменять это дичь должно быть просто Where(reader)
        return new DomainDataReader(new LimitDbDataReader(reader, count));
    }

    public static DomainDataReader CollectMeta(this DomainDataReader reader, DataMetaContainer metaContainer)
    {
        // TODO: поменять это дичь должно быть просто Where(reader)
        return new DomainDataReader(new MetaCollectingDataReader(reader, metaContainer));
    }

}

using System.Data.Common;
using Loader.Core.Data.AutoCast;

namespace Loader.Core.Data;

/// <summary>
/// Точка входа для stream-операций поверх стандартного DbDataReader.
/// </summary>
public static class DbDataReaderExtensions
{

    public static DomainDataReader Normalize(this DbDataReader reader)
    {
        return reader is DomainDataReader domainReader
            ? domainReader
            : new NormalizingDomainDataReader(reader);
    }

    public static DomainDataReader Where(this DomainDataReader reader, Func<Row, bool> predicate)
    {
        return new WhereDomainDataReader(reader, predicate);
    }

    public static DomainDataReader Limit(this DomainDataReader reader, int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Limit must be greater than or equal to zero.");
        }

        return new LimitDbDataReader(reader, count);
    }

    public static DomainDataReader CollectMeta(this DomainDataReader reader, DataMetaContainer metaContainer)
    {
        return new MetaCollectingDataReader(reader, metaContainer);
    }

    public static DomainDataReader CollectAutoCast(this DomainDataReader reader, AutoCastAnalyzer analyzer)
    {
        return new AutoCastAnalyzingDataReader(reader, analyzer);
    }

    public static DomainDataReader AutoCast(this DomainDataReader reader, AutoCastSchema schema)
    {
        return new AutoCastDataReader(reader, schema);
    }

}

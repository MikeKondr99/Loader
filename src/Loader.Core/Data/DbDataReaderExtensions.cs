using System.Data.Common;

namespace Loader.Core.Data;

/// <summary>
/// Точка входа для stream-операций поверх стандартного DbDataReader.
/// </summary>
public static class DbDataReaderExtensions
{
    public static DomainDataReader AsTyped(this DbDataReader reader)
    {
        return reader.AsDomain();
    }

    public static DomainDataReader AsDomain(this DbDataReader reader)
    {
        return reader.Normalize(new NormalizeConfig
        {
            Limit = null
        });
    }

    public static DomainDataReader Normalize(this DbDataReader reader, NormalizeConfig config)
    {
        var configuredReader = ApplyLimit(reader, config);
        return new DomainDataReader(configuredReader);
    }

    public static DomainDataReader Normilize(this DbDataReader reader, NormalizeConfig config)
    {
        return reader.Normalize(config);
    }

    public static DomainDataReader Where(this DomainDataReader reader, Func<Row, bool> predicate)
    {
        return new DomainDataReader(new WhereDomainDataReader(reader, predicate));
    }

    public static DomainDataReader Limit(this DomainDataReader reader, int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Limit must be greater than or equal to zero.");
        }

        return new DomainDataReader(new LimitDbDataReader(reader, count));
    }

    public static DomainDataReader CollectMeta(this DomainDataReader reader, DataMetaContainer metaContainer)
    {
        return new DomainDataReader(new MetaCollectingDataReader(reader, metaContainer));
    }

    private static DbDataReader ApplyLimit(DbDataReader reader, NormalizeConfig config)
    {
        if (config.Limit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(config.Limit), config.Limit, "Limit must be greater than or equal to zero.");
        }

        return config.Limit is null
            ? reader
            : new LimitDbDataReader(reader, config.Limit.Value);
    }
}

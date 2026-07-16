using ClickHouse.Client.ADO;
using ClickHouse.Client.Copy;
using Loader.Core.Decorators;
using Loader.Core.Sources;

namespace Loader.Core.Writers.ClickHouse;

/// <summary>
/// Writer доменного потока в ClickHouse.
/// Создание таблицы, выбор типов и bulk insert разделены: SQL собирают отдельные builders,
/// а бинарную запись выполняет ClickHouse.Client через ClickHouseBulkCopy.
/// </summary>
public sealed class ClickHouseWriter
{
    public async ValueTask WriteAsync(
        IDatabaseSource source,
        DomainDataReader reader,
        ClickHouseWriteOptions options,
        DataMetaContainer? meta = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new ClickHouseConnection(source.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var createSql = BuildCreateTableSql(reader, options, meta);

        try
        {
            // 1. Создаем таблицу с типами, выбранными по доменной схеме и meta.
            await using var command = connection.CreateCommand();
            command.CommandText = createSql;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            // 2. Передаем поток строк в ClickHouseBulkCopy, который пишет через binary protocol.
            using var bulkCopy = new ClickHouseBulkCopy(connection)
            {
                DestinationTableName = options.TableName.ToBulkCopyName(),
                ColumnNames = reader.DataSchema.Fields.Select(static field => field.Name).ToArray(),
                BatchSize = options.BatchSize,
                MaxDegreeOfParallelism = options.MaxDegreeOfParallelism
            };

            await bulkCopy.InitAsync().ConfigureAwait(false);
            await bulkCopy.WriteToServerAsync(reader).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new DbExecutionException("clickhouse", createSql, ex);
        }
    }

    public string BuildCreateTableSql(
        DomainDataReader reader,
        ClickHouseWriteOptions options,
        DataMetaContainer? meta = null)
    {
        var typeResolver = new ClickHouseColumnTypeResolver(options);
        return ClickHouseSql.CreateTable(reader.DataSchema, meta, options, typeResolver);
    }
}

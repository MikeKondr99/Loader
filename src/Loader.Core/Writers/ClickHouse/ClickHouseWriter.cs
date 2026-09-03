using System.Diagnostics;
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
        Activity.Current?
            .SetTag("db.system", "clickhouse")
            .SetTag("db.statement.create_table", createSql);

        try
        {
            // 1. Создаем таблицу с типами, выбранными по доменной схеме и meta.
            await using var command = connection.CreateCommand();
            command.CommandText = createSql;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new DbExecutionException("clickhouse", createSql, ex);
        }

        var insertSql = BuildInsertContextSql(reader, options);
        Activity.Current?
            .SetTag("db.statement.insert", insertSql);

        try
        {
            // 2. Передаем поток строк в ClickHouseBulkCopy, который пишет через binary protocol.
            using var bulkCopy = new ClickHouseBulkCopy(connection)
            {
                DestinationTableName = options.TableName.ToBulkCopyName(),
                ColumnNames = reader.DataSchema.Fields.Select(static field => field.Name).ToArray(),
                BatchSize = options.BatchSize,
                MaxDegreeOfParallelism = options.MaxDegreeOfParallelism
            };

            await bulkCopy.InitAsync().ConfigureAwait(false);

            // BulkCopy пишет в физические CH-типы. Доменные значения адаптируются здесь,
            // не меняя доменную схему reader-а: например Time пишем как DateTime с датой 1970-01-01.
            await bulkCopy.WriteToServerAsync(ClickHouseWriteDataReader.Wrap(reader)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new DbExecutionException("clickhouse", insertSql, ex);
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

    private static string BuildInsertContextSql(DomainDataReader reader, ClickHouseWriteOptions options)
    {
        var columns = string.Join(", ", reader.DataSchema.Fields.Select(static field => $"`{field.Name}`"));
        return $"INSERT INTO {options.TableName.ToSql()} ({columns})";
    }
}

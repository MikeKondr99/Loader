using System.Diagnostics;
using System.Data.Common;
using ClickHouse.Client.ADO;
using ClickHouse.Driver;
using DriverConnectionStringBuilder = ClickHouse.Driver.ADO.ClickHouseConnectionStringBuilder;
using Loader.Core.Decorators;
using Loader.Core.Sources;

namespace Loader.Core.Writers.ClickHouse;

/// <summary>
/// Writer доменного потока в ClickHouse.
/// Создание таблицы, выбор типов и bulk insert разделены: SQL собирают отдельные builders,
/// а бинарную запись выполняет ClickHouse.Driver через ClickHouseClient.InsertBinaryAsync.
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
        var columnTypes = BuildColumnTypes(reader, options, meta);

        try
        {
            // InsertBinaryAsync пишет в физические CH-типы. Доменные значения адаптируются здесь,
            // не меняя доменную схему reader-а: например Time пишем как DateTime с датой 1970-01-01.
            await InsertBatchesAsync(
                    source.ConnectionString,
                    options,
                    reader.DataSchema.Fields.Select(static field => field.Name).ToArray(),
                    columnTypes,
                    ClickHouseWriteDataReader.Wrap(reader),
                    cancellationToken)
                .ConfigureAwait(false);
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

    private static async Task InsertBatchesAsync(
        string connectionString,
        ClickHouseWriteOptions options,
        IReadOnlyList<string> columns,
        IReadOnlyDictionary<string, string> columnTypes,
        DbDataReader reader,
        CancellationToken cancellationToken)
    {
        var parallelism = Math.Max(1, options.MaxDegreeOfParallelism);
        var batchSize = Math.Max(1, options.BatchSize / parallelism);
        var runningTasks = new List<Task<long>>(parallelism);

        while (true)
        {
            var batch = await ReadBatchAsync(reader, columns.Count, batchSize, cancellationToken).ConfigureAwait(false);
            if (batch.Count == 0)
            {
                break;
            }

            runningTasks.Add(InsertBatchAsync(connectionString, options.TableName, columns, columnTypes, batch, cancellationToken));
            if (runningTasks.Count < parallelism)
            {
                continue;
            }

            var completedTask = await Task.WhenAny(runningTasks).ConfigureAwait(false);
            await completedTask.ConfigureAwait(false);
            runningTasks.Remove(completedTask);
        }

        await Task.WhenAll(runningTasks).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<object[]>> ReadBatchAsync(
        DbDataReader reader,
        int fieldCount,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var rows = new List<object[]>(batchSize);
        while (rows.Count < batchSize && await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var values = new object[fieldCount];
            reader.GetValues(values);
            for (var index = 0; index < values.Length; index++)
            {
                if (values[index] == DBNull.Value)
                {
                    values[index] = null!;
                }
            }

            rows.Add(values);
        }

        return rows;
    }

    private static async Task<long> InsertBatchAsync(
        string connectionString,
        ClickHouseTableName tableName,
        IReadOnlyList<string> columns,
        IReadOnlyDictionary<string, string> columnTypes,
        IReadOnlyList<object[]> rows,
        CancellationToken cancellationToken)
    {
        using var client = CreateInsertClient(connectionString);
        return await client.InsertBinaryAsync(
                tableName.ToSql(),
                columns,
                rows,
                new InsertOptions
                {
                    BatchSize = rows.Count,
                    MaxDegreeOfParallelism = 1,
                    ColumnTypes = columnTypes,
                    UseSession = false
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static ClickHouseClient CreateInsertClient(string connectionString)
    {
        var builder = new DriverConnectionStringBuilder(connectionString)
        {
            UseSession = false,
            JsonReadMode = JsonReadMode.None,
            JsonWriteMode = JsonWriteMode.None
        };

        return new ClickHouseClient(builder.ConnectionString);
    }

    private static IReadOnlyDictionary<string, string> BuildColumnTypes(
        DomainDataReader reader,
        ClickHouseWriteOptions options,
        DataMetaContainer? meta)
    {
        var typeResolver = new ClickHouseColumnTypeResolver(options);
        return reader.DataSchema.Fields.ToDictionary(
            static field => field.Name,
            field => typeResolver.Resolve(field, FindMeta(field, meta)),
            StringComparer.Ordinal);
    }

    private static DataColumnMeta? FindMeta(DataField field, DataMetaContainer? meta)
    {
        if (meta is null)
        {
            return null;
        }

        return field.Ordinal < meta.Columns.Count
            ? meta.Columns[field.Ordinal]
            : null;
    }
}

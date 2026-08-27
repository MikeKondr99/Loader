using System.Data.Common;
using ClickHouse.Client.ADO;
using Loader.Core.Decorators;
using Loader.Core.Models;
using Loader.Core.Sources;
using Loader.Core.Writers.ClickHouse;
using Loader.Lang.Statements;
using Loader.Query.Resolve;

namespace Loader.Script.Execution;

public class TempTableMaterializer
{
    public async ValueTask<TemporaryClickHouseTable> MaterializeAsync(
        ScriptContext context,
        LoadStatement statement,
        ReaderLoadFromSource source,
        CancellationToken cancellationToken = default)
    {
        await using var providerReader = await OpenProviderReaderAsync(statement, source, cancellationToken)
            .ConfigureAwait(false);

        await using var stageNameReader = providerReader.AbstractColumns();

        await using var stageReader = LimitSourceRows(
            NormalizeForTempTable(stageNameReader, source),
            ToInt32(statement.First, nameof(statement.First)));

        ValidateMappedTempTableSchema(statement, stageReader.DataSchema.Fields.Count);

        var tempTable = CreatePhysicalTempTableName(context);

        using (var tempTableActivity = LoadScriptTelemetry.ActivitySource.StartActivity("LoadStatement.TempTableWrite"))
        {
            tempTableActivity?
                .SetTag("load.table_name", statement.TableName)
                .SetTag("load.source_provider", statement.SourceCall.Name)
                .SetTag("load.temp_table", tempTable.Table);

            try
            {
                var rowCount = await WriteTempTableAsync(context, stageReader, tempTable, cancellationToken).ConfigureAwait(false);
                await context.Logger.SourceRowsLoadedAsync(rowCount, cancellationToken).ConfigureAwait(false);
            }
            catch (LoadScriptStageException)
            {
                throw;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw new LoadScriptExecutionException(
                    LoadScriptStage.TempTableWrite,
                    $"Не удалось загрузить данные во временную таблицу: {exception.Message}",
                    statement.LoadSpan ?? statement.FromSpan,
                    exception);
            }
        }

        return CreateTempTableResult(context, stageNameReader, stageReader, tempTable);
    }

    private static async ValueTask<DbDataReader> OpenProviderReaderAsync(
        LoadStatement statement,
        ReaderLoadFromSource source,
        CancellationToken cancellationToken)
    {
        try
        {
            return await source.OpenReaderAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (LoadScriptStageException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new LoadScriptExecutionException(
                LoadScriptStage.SourceOpen,
                $"Не удалось открыть источник данных: {exception.Message}",
                statement.SqlPart?.Span ?? statement.FromSpan,
                exception);
        }
    }

    private static DomainDataReader NormalizeForTempTable(
        RenameColumnDataReader stageNameReader,
        ReaderLoadFromSource source)
    {
        return stageNameReader.Normalize(new NormalizeOptions
        {
            Buffer = source.RequiresBuffer
        });
    }

    private static DomainDataReader LimitSourceRows(DomainDataReader reader, int? limit)
    {
        return limit is null
            ? reader
            : reader.Limit(limit.Value);
    }

    private static int? ToInt32(long? value, string name)
    {
        if (value is null)
        {
            return null;
        }

        if (value < 0 || value > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Value must fit Int32.");
        }

        return (int)value.Value;
    }

    protected virtual async ValueTask<long> WriteTempTableAsync(
        ScriptContext context,
        DomainDataReader stageReader,
        ClickHouseTableName tempTable,
        CancellationToken cancellationToken)
    {
        var source = new ConnectionStringSource
        {
            ConnectionString = context.TargetConnectionString
        };
        var meta = new DataMetaContainer();
        await using var metaReader = stageReader.CollectMeta(meta);
        await new ClickHouseWriter()
            .WriteAsync(
                source,
                metaReader,
                new ClickHouseWriteOptions
                {
                    TableName = tempTable,
                    Engine = "Log"
                },
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return meta.RowCount;
    }

    protected virtual async ValueTask DropTempTableAsync(
        ScriptContext context,
        ClickHouseTableName tempTable,
        CancellationToken cancellationToken)
    {
        await using var connection = new ClickHouseConnection(context.TargetConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE IF EXISTS {tempTable.ToSql()}";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask DropTempTableBestEffortAsync(
        ScriptContext context,
        ClickHouseTableName tempTable)
    {
        try
        {
            await DropTempTableAsync(context, tempTable, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _ = exception;
        }
    }

    private TemporaryClickHouseTable CreateTempTableResult(
        ScriptContext context,
        RenameColumnDataReader stageNameReader,
        DomainDataReader stageReader,
        ClickHouseTableName tempTable)
    {
        return new TemporaryClickHouseTable(
            tempTable,
            stageReader.DataSchema,
            stageNameReader.OriginalNames.ToArray(),
            () => DropTempTableBestEffortAsync(context, tempTable));
    }

    private static ClickHouseTableName CreatePhysicalTempTableName(ScriptContext context)
    {
        return new ClickHouseTableName
        {
            Table = $"{context.Options.TempTablePrefix}{Guid.NewGuid():N}"
        };
    }

    private static void ValidateMappedTempTableSchema(
        LoadStatement statement,
        int fieldCount)
    {
        if (!statement.IsMapped || statement.Fields is not null || fieldCount == 2)
        {
            return;
        }

        throw new QueryResolutionException(
            $"MAPPED LOAD * получил source с {fieldCount} полями, ожидалось 2: key и value.",
            statement.KindSpan ?? statement.LoadSpan);
    }
}

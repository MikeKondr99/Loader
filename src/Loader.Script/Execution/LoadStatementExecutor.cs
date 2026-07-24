using System.Data.Common;
using Loader.Core.Decorators;
using Loader.Core.Sources;
using Loader.Core.Writers.ClickHouse;
using Loader.Lang.Statements;
using Microsoft.Extensions.Logging;

namespace Loader.Script;

public class LoadStatementExecutor
{
    public ILoadProviderResolver ProviderResolver { get; init; } = new LoadProviderResolver();

    public string TempTablePrefix { get; init; } = "loader_script_temp_";

    public async ValueTask<LoadTempTableResult> LoadTempTableAsync(
        ScriptContext context,
        LoadStatement statement,
        CancellationToken cancellationToken = default)
    {
        using var activity = LoadScriptTelemetry.ActivitySource.StartActivity("LoadStatement");
        activity?.SetTag("load.table_name", statement.TableName);

        // 1. По FROM и options выбираем provider.
        var source = await ResolveProviderAsync(context, statement, cancellationToken).ConfigureAwait(false);
        activity?.SetTag("load.source_kind", source.Kind);

        // 2. Открываем provider reader.
        await using var providerReader = await OpenProviderReaderAsync(context, statement, source, cancellationToken)
            .ConfigureAwait(false);

        // 3. Заменяем имена колонок на column1, column2...
        await using var stageNameReader = providerReader.AbstractColumns();

        // 4. Нормализуем поток перед записью в ClickHouse temp table.
        await using var stageReader = NormalizeForTempTable(stageNameReader, source);

        // 5. Создаем temp table name.
        var tempTable = CreateTempTableName(statement);
        activity?.SetTag("load.temp_table", tempTable.Table);

        // 6. Потоково пишем stage reader в temp table.
        using (var tempTableActivity = LoadScriptTelemetry.ActivitySource.StartActivity("LoadStatement.TempTableWrite"))
        {
            tempTableActivity?.SetTag("load.table_name", statement.TableName);
            tempTableActivity?.SetTag("load.source_kind", source.Kind);
            tempTableActivity?.SetTag("load.temp_table", tempTable.Table);

            await WriteTempTableAsync(context, stageReader, tempTable, cancellationToken).ConfigureAwait(false);
        }

        // 7. Возвращаем данные, нужные следующему шагу LOAD pipeline.
        return CreateTempTableResult(stageNameReader, stageReader, tempTable);
    }

    private async ValueTask<LoadProviderSource> ResolveProviderAsync(
        ScriptContext context,
        LoadStatement statement,
        CancellationToken cancellationToken)
    {
        context.Logger.ResolvingLoadProvider(statement.Source);
        var source = await ProviderResolver
            .ResolveAsync(statement, context, cancellationToken)
            .ConfigureAwait(false);
        context.Logger.LoadProviderResolved(source.Kind);
        return source;
    }

    private static async ValueTask<DbDataReader> OpenProviderReaderAsync(
        ScriptContext context,
        LoadStatement statement,
        LoadProviderSource source,
        CancellationToken cancellationToken)
    {
        context.Logger.OpeningLoadReader(statement.Source);
        return await source.OpenReaderAsync(cancellationToken).ConfigureAwait(false);
    }

    private static DomainDataReader NormalizeForTempTable(
        RenameColumnDataReader stageNameReader,
        LoadProviderSource source)
    {
        return stageNameReader.Normalize(new NormalizeOptions
        {
            Buffer = source.RequiresBuffer
        });
    }

    protected virtual async ValueTask WriteTempTableAsync(
        ScriptContext context,
        DomainDataReader stageReader,
        ClickHouseTableName tempTable,
        CancellationToken cancellationToken)
    {
        context.Logger.LoadingTempTable(tempTable.ToSql());

        var source = new ConnectionStringSource
        {
            ConnectionString = context.TargetConnectionString
        };
        await new ClickHouseWriter()
            .WriteAsync(
                source,
                stageReader,
                new ClickHouseWriteOptions { TableName = tempTable },
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        context.Logger.TempTableLoaded(tempTable.ToSql());
    }

    private static LoadTempTableResult CreateTempTableResult(
        RenameColumnDataReader stageNameReader,
        DomainDataReader stageReader,
        ClickHouseTableName tempTable)
    {
        return new LoadTempTableResult
        {
            TableName = tempTable,
            Schema = stageReader.DataSchema,
            OriginalColumnNames = stageNameReader.OriginalNames.ToArray()
        };
    }

    private ClickHouseTableName CreateTempTableName(LoadStatement statement)
    {
        var stablePart = statement.TableName is null
            ? string.Empty
            : statement.TableName + "_";
        return new ClickHouseTableName
        {
            Table = $"{TempTablePrefix}{stablePart}{Guid.NewGuid():N}"
        };
    }
}

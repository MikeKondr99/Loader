using System.Data.Common;
using System.Text;
using ClickHouse.Client.ADO;
using Loader.Core.Decorators;
using Loader.Core.Providers.ClickHouse;
using Loader.Core.Providers.Sql;
using Loader.Core.Sources;
using Loader.Core.Writers.ClickHouse;
using Loader.Lang.Statements;
using Loader.Query.Compile;
using Loader.Query.Functions;
using Loader.Query.Models;
using Loader.Query.Resolve;
using CoreDataType = Loader.Core.Models.DataType;
using QueryDataType = Loader.Query.Models.DataType;
using QueryModel = Loader.Query.Models.Query;
using QueryTemplate = Loader.Query.Template.Template;

namespace Loader.Script.Execution;

/// <summary>
/// Исполнитель одной LOAD-инструкции без собственного состояния.
/// Состояние и настройки конкретного запуска хранятся в <see cref="ScriptContext"/>.
/// </summary>
public class LoadStatementExecutor
{
    public ILoadProviderResolver ProviderResolver { get; init; } = new LoadProviderResolver();

    public TempTableMaterializer TempTableMaterializer { get; init; } = new();

    public virtual async ValueTask<LoadedTable> ExecuteAsync(
        ScriptContext context,
        LoadStatement statement,
        CancellationToken cancellationToken = default)
    {
        ValidateMappedStatementFields(statement);

        await context.Logger.LoadTableStartedAsync(statement.TableName, cancellationToken).ConfigureAwait(false);

        // 1. Готовим FROM: либо получаем SQL напрямую, либо грузим внешний source в temp table.
        await using var preparedSource = await new LoadFromPreparer(ProviderResolver, TempTableMaterializer)
            .PrepareAsync(context, statement, cancellationToken)
            .ConfigureAwait(false);
        ValidateMappedPreparedSourceSchema(statement, preparedSource.Fields.Count);

        // 2. LOAD выражения превращаем в типизированный Query поверх подготовленного source.
        var (resolvedQuery, querySql) = BuildResolvedQuerySql(context, statement, preparedSource);

        // 3. Query выполняем в ClickHouse и результат потоково сохраняем в final table.
        await using var finalTable = CreateFinalTable(context);
        var finalRowCount = await MaterializeFinalTableWithTelemetryAsync(context, statement, querySql, finalTable.TableName, cancellationToken)
            .ConfigureAwait(false);
        await context.Logger.TransformationRowsLoadedAsync(finalRowCount, cancellationToken).ConfigureAwait(false);

        // 4. Пока meta пустая: фиксируем только имя таблицы и поля из resolved output.
        var loadedTable = CreateLoadedTable(statement, resolvedQuery, finalTable.TableName);
        context.AddLoadedTable(loadedTable);
        finalTable.Commit();
        return loadedTable;
    }

    public async ValueTask<TemporaryClickHouseTable> LoadTempTableAsync(
        ScriptContext context,
        LoadStatement statement,
        ReaderLoadFromSource source,
        CancellationToken cancellationToken = default)
    {
        await using var providerReader = await OpenProviderReaderAsync(context, statement, source, cancellationToken)
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
        ScriptContext context,
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
                CreateWriteOptions(tempTable, LoadClickHouseTableKind.Temp),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return meta.RowCount;
    }

    private ResolvedQuerySql BuildResolvedQuerySql(
        ScriptContext context,
        LoadStatement statement,
        PreparedLoadSource source)
    {
        using var activity = LoadScriptTelemetry.ActivitySource.StartActivity("LoadStatement.QueryBuild");
        activity?
            .SetTag("load.table_name", statement.TableName);

        ThrowIfDuplicateSelectAliases(statement);
        var query = BuildQuery(statement, source);
        var resolvedQuery = ResolveQuery(query, CreateExpressionResolutionContext(context));
        var querySql = CompileQuery(statement, resolvedQuery);
        return new ResolvedQuerySql(resolvedQuery, querySql);
    }

    protected virtual ExpressionResolutionContext CreateExpressionResolutionContext(ScriptContext context)
    {
        return new ScriptExpressionResolutionContext(context);
    }

    private static QueryModel BuildQuery(LoadStatement statement, PreparedLoadSource source)
    {
        return new QueryModel
        {
            Source = BuildQuerySource(source),
            Select = BuildSelect(statement),
            Where = statement.Where,
            GroupBy = statement.GroupBy ?? [],
            OrderBy = BuildOrderBy(statement),
            Limit = ToUInt32(statement.Limit, nameof(statement.Limit)),
            LimitSpan = statement.LimitPart?.Span,
            Offset = ToUInt32(statement.Offset, nameof(statement.Offset))
        };
    }

    private static QuerySource BuildQuerySource(PreparedLoadSource source)
    {
        ThrowIfDuplicateSourceNames(source.Fields.Select(static field => field.Name).ToArray());
        return new QuerySource
        {
            Sql = source.Sql,
            Alias = source.Alias,
            Fields = source.Fields.Select(field => new Field
            {
                Alias = field.Name,
                Template = QueryTemplate.Text($"{source.Alias}.`{field.PhysicalName}`"),
                Type = new FieldType
                {
                    DataType = ToQueryDataType(field.DataType),
                    CanBeNull = field.CanBeNull
                }
            }).ToArray()
        };
    }

    private static IReadOnlyList<SelectItem> BuildSelect(LoadStatement statement)
    {
        if (statement.Fields is null)
        {
            return [];
        }

        return statement.Fields.Select(static field => new SelectItem
        {
            Alias = field.Name,
            Expression = field.Expression
        }).ToArray();
    }

    private static IReadOnlyList<OrderItem> BuildOrderBy(LoadStatement statement)
    {
        if (statement.OrderBy is null)
        {
            return [];
        }

        return statement.OrderBy.Select(static field => new OrderItem
        {
            Expression = field.Expression,
            Direction = field.Direction switch
            {
                LoadOrderDirection.Ascending => OrderDirection.Asc,
                LoadOrderDirection.Descending => OrderDirection.Desc,
                var unknown => throw new ArgumentOutOfRangeException(nameof(field.Direction), unknown, null)
            }
        }).ToArray();
    }

    private static ResolvedQuery ResolveQuery(QueryModel query, ExpressionResolutionContext expressionContext)
    {
        var result = new QueryResolver().Resolve(query, ClickHouseFunctions.CreateResolver(), expressionContext);
        if (result.IsSuccess)
        {
            return result.Value!;
        }

        throw new QueryResolutionException(result.Errors);
    }

    private static string CompileQuery(LoadStatement statement, ResolvedQuery query)
    {
        try
        {
            return new ClickHouseQueryCompiler
            {
                ExpressionCompiler = new ExpressionCompiler()
            }.Compile(query);
        }
        catch (LoadScriptStageException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new LoadScriptExecutionException(
                LoadScriptStage.QueryCompilation,
                $"Не удалось скомпилировать LOAD query: {exception.Message}",
                statement.LoadSpan ?? statement.FromSpan,
                exception);
        }
    }

    private async ValueTask<long> MaterializeFinalTableWithTelemetryAsync(
        ScriptContext context,
        LoadStatement statement,
        string querySql,
        ClickHouseTableName finalTable,
        CancellationToken cancellationToken)
    {
        using var activity = LoadScriptTelemetry.ActivitySource.StartActivity("LoadStatement.FinalTableWrite");
        activity?
            .SetTag("load.table_name", statement.TableName)
            .SetTag("load.final_table", finalTable.Table);

        try
        {
            await context.Logger.TransformationWriteStartedAsync(cancellationToken).ConfigureAwait(false);
            return await MaterializeFinalTableAsync(context, statement, querySql, finalTable, cancellationToken).ConfigureAwait(false);
        }
        catch (LoadScriptStageException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new LoadScriptExecutionException(
                LoadScriptStage.FinalTableWrite,
                $"Не удалось материализовать финальную таблицу: {exception.Message}",
                statement.LoadSpan ?? statement.FromSpan,
                exception);
        }
    }

    protected virtual async ValueTask<long> MaterializeFinalTableAsync(
        ScriptContext context,
        LoadStatement statement,
        string querySql,
        ClickHouseTableName finalTable,
        CancellationToken cancellationToken)
    {
        var source = new ConnectionStringSource
        {
            ConnectionString = context.TargetConnectionString
        };
        await using var rawReader = await new ClickHouseProvider()
            .OpenReaderAsync(source, new SqlTableConfig { Sql = querySql }, cancellationToken)
            .ConfigureAwait(false);
        await using var finalNameReader = rawReader.AbstractColumns();
        await using var finalReader = finalNameReader.Normalize();

        var meta = new DataMetaContainer();
        await using var metaReader = finalReader.CollectMeta(meta);
        await new ClickHouseWriter()
            .WriteAsync(
                source,
                metaReader,
                CreateWriteOptions(
                    finalTable,
                    statement.IsMapped ? LoadClickHouseTableKind.Mapped : LoadClickHouseTableKind.Final),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return meta.RowCount;
    }

    private static ClickHouseWriteOptions CreateWriteOptions(
        ClickHouseTableName tableName,
        LoadClickHouseTableKind kind)
    {
        return new ClickHouseWriteOptions
        {
            TableName = tableName,
            Engine = kind switch
            {
                // Log не требует ORDER BY и подходит для stage/intermediate таблиц.
                LoadClickHouseTableKind.Temp => "Log",

                // Пока стратегия MergeTree ORDER BY для пользовательских final tables не определена.
                LoadClickHouseTableKind.Final => "Log",

                // ApplyMap читает mapping через joinGetOrNull, поэтому нужна ClickHouse Join-таблица.
                LoadClickHouseTableKind.Mapped => "Join(ANY, LEFT, `column1`)",
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
            }
        };
    }

    protected virtual async ValueTask DropTempTableAsync(
        ScriptContext context,
        ClickHouseTableName tempTable,
        CancellationToken cancellationToken)
    {
        await DropTableAsync(context, tempTable, cancellationToken).ConfigureAwait(false);
    }

    protected virtual async ValueTask DropFinalTableAsync(
        ScriptContext context,
        ClickHouseTableName finalTable,
        CancellationToken cancellationToken)
    {
        await DropTableAsync(context, finalTable, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask DropTableAsync(
        ScriptContext context,
        ClickHouseTableName tableName,
        CancellationToken cancellationToken)
    {
        await using var connection = new ClickHouseConnection(context.TargetConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = new StringBuilder()
            .Append("DROP TABLE IF EXISTS ")
            .Append(tableName.ToSql())
            .ToString();
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
            // Best-effort cleanup: исходная ошибка LOAD важнее ошибки удаления temp table.
        }
    }

    private async ValueTask DropFinalTableBestEffortAsync(
        ScriptContext context,
        ClickHouseTableName finalTable)
    {
        try
        {
            await DropFinalTableAsync(context, finalTable, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Best-effort cleanup: исходная ошибка LOAD важнее ошибки удаления final table.
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

    private FinalClickHouseTable CreateFinalTable(ScriptContext context)
    {
        var finalTable = CreatePhysicalFinalTableName(context);
        return new FinalClickHouseTable(
            finalTable,
            () => DropFinalTableBestEffortAsync(context, finalTable));
    }

    private static ClickHouseTableName CreatePhysicalTempTableName(ScriptContext context)
    {
        return new ClickHouseTableName
        {
            Table = $"{context.Options.TempTablePrefix}{Guid.NewGuid():N}"
        };
    }

    private static ClickHouseTableName CreatePhysicalFinalTableName(ScriptContext context)
    {
        return new ClickHouseTableName
        {
            Table = $"{context.Options.FinalTablePrefix}{Guid.NewGuid():N}"
        };
    }

    private static LoadedTable CreateLoadedTable(
        LoadStatement statement,
        ResolvedQuery resolvedQuery,
        ClickHouseTableName finalTable)
    {
        return new LoadedTable
        {
            Name = finalTable,
            Alias = statement.TableName,
            Kind = statement.Kind switch
            {
                LoadTableKind.Temp => LoadedTableKind.Temp,
                LoadTableKind.Mapped => LoadedTableKind.Mapped,
                _ => LoadedTableKind.Normal
            },
            Fields = resolvedQuery.OutputFields.Select(field => new LoadedTableField
            {
                Name = field.Alias,
                DataType = ToCoreDataType(field.Type.DataType),
                CanBeNull = field.Type.CanBeNull
            }).ToList()
        };
    }

    private static uint? ToUInt32(long? value, string name)
    {
        if (value is null)
        {
            return null;
        }

        if (value < 0 || value > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Value must fit UInt32.");
        }

        return (uint)value.Value;
    }

    private static void ThrowIfDuplicateSourceNames(IReadOnlyList<string> names)
    {
        var duplicate = names
            .GroupBy(static name => name, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new QueryResolutionException($"LOAD source field '{duplicate.Key}' is duplicated.");
        }
    }

    private static void ThrowIfDuplicateSelectAliases(LoadStatement statement)
    {
        if (statement.Fields is null)
        {
            return;
        }

        var aliases = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in statement.Fields)
        {
            if (aliases.Add(field.Name))
            {
                continue;
            }

            throw new QueryResolutionException(
                $"LOAD select alias '{field.Name}' is duplicated.",
                field.Span);
        }
    }

    private static void ValidateMappedStatementFields(LoadStatement statement)
    {
        if (!statement.IsMapped || statement.Fields is null || statement.Fields.Count == 2)
        {
            return;
        }

        throw new QueryResolutionException(
            "MAPPED LOAD должен возвращать ровно два поля: key и value.",
            statement.KindSpan ?? statement.LoadSpan);
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

    private static void ValidateMappedPreparedSourceSchema(
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

    private static QueryDataType ToQueryDataType(CoreDataType dataType)
    {
        return dataType switch
        {
            CoreDataType.Text => QueryDataType.Text,
            CoreDataType.Integer => QueryDataType.Integer,
            CoreDataType.Number => QueryDataType.Number,
            CoreDataType.DateTime => QueryDataType.DateTime,
            CoreDataType.Date => QueryDataType.Date,
            CoreDataType.Time => QueryDataType.Time,
            CoreDataType.Boolean => QueryDataType.Boolean,
            _ => QueryDataType.Unknown
        };
    }

    private static CoreDataType ToCoreDataType(QueryDataType dataType)
    {
        return dataType switch
        {
            QueryDataType.Text => CoreDataType.Text,
            QueryDataType.Integer => CoreDataType.Integer,
            QueryDataType.Number => CoreDataType.Number,
            QueryDataType.DateTime => CoreDataType.DateTime,
            QueryDataType.Date => CoreDataType.Date,
            QueryDataType.Time => CoreDataType.Time,
            QueryDataType.Boolean => CoreDataType.Boolean,
            _ => CoreDataType.Text
        };
    }

    private readonly record struct ResolvedQuerySql(ResolvedQuery Query, string Sql);
}

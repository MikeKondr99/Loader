using System.Diagnostics;
using System.Text;
using ClickHouse.Client.ADO;
using Loader.Core.Decorators;
using Loader.Core.Tasks;
using Loader.Core.Writers.ClickHouse;
using Loader.Lang.Expressions;
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
        var tableName = ValidateTableName(context, statement);
        statement = ResolveLoadFieldAliases(statement);
        ValidateMappedStatementFields(statement);

        await context.Logger.LoadTableStartedAsync(tableName, cancellationToken).ConfigureAwait(false);

        // 1. Готовим FROM: либо получаем SQL напрямую, либо грузим внешний source в temp table.
        await using var preparedSource = await new LoadFromPreparer(ProviderResolver, TempTableMaterializer)
            .PrepareAsync(context, statement, cancellationToken)
            .ConfigureAwait(false);
        ValidateMappedPreparedSourceSchema(statement, preparedSource.Fields.Count);

        // 2. LOAD выражения превращаем в типизированный Query поверх подготовленного source.
        var (resolvedQuery, querySql) = BuildResolvedQuerySql(context, statement, tableName, preparedSource);

        // 3. Query выполняем в ClickHouse и server-side сохраняем результат в final table.
        await using var finalTable = CreateFinalTable(context);
        var finalRowCount = await MaterializeFinalTableWithTelemetryAsync(context, statement, tableName, querySql, finalTable.TableName, cancellationToken)
            .ConfigureAwait(false);

        // 4. Пока meta пустая: фиксируем только имя таблицы и поля из resolved output.
        var loadedTable = CreateLoadedTable(statement, tableName, resolvedQuery, finalTable.TableName, finalRowCount);
        context.AddLoadedTable(loadedTable);
        finalTable.Commit();
        return loadedTable;
    }

    private ResolvedQuerySql BuildResolvedQuerySql(
        ScriptContext context,
        LoadStatement statement,
        string tableName,
        PreparedLoadSource source)
    {
        using var activity = LoadScriptTelemetry.ActivitySource.StartActivity("LoadStatement.QueryBuild");
        activity?
            .SetTag("load.table_name", tableName);

        var query = BuildQuery(statement, source);
        var resolvedQuery = ResolveQueryWithTelemetry(context, statement, tableName, source, query);
        var querySql = CompileQuery(statement, resolvedQuery);
        activity?
            .SetSanitizedTag("load.query_sql", querySql);
        return new ResolvedQuerySql(resolvedQuery, querySql);
    }

    private ResolvedQuery ResolveQueryWithTelemetry(
        ScriptContext context,
        LoadStatement statement,
        string tableName,
        PreparedLoadSource source,
        QueryModel query)
    {
        using var activity = LoadScriptTelemetry.ActivitySource.StartActivity("LoadStatement.QueryResolve");
        activity?
            .SetTag("load.table_name", tableName)
            .SetTag("load.source_field_count", source.Fields.Count)
            .SetTag("load.select_count", query.Select.Count)
            .SetTag("load.group_by_count", query.GroupBy.Count)
            .SetTag("load.order_by_count", query.OrderBy.Count);

        var resolvedQuery = ResolveQuery(query, CreateExpressionResolutionContext(context));
        activity?
            .SetTag("load.output_field_count", resolvedQuery.OutputFields.Count);
        return resolvedQuery;
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
            Alias = field.Name!,
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
        string tableName,
        string querySql,
        ClickHouseTableName finalTable,
        CancellationToken cancellationToken)
    {
        using var activity = LoadScriptTelemetry.ActivitySource.StartActivity("LoadStatement.FinalTableWrite");
        activity?
            .SetTag("load.table_name", tableName)
            .SetTag("load.final_table", finalTable.Table);

        try
        {
            var heartbeatMessageId = Guid.NewGuid().ToString("N");
            await context.Logger.TransformationWriteStartedAsync(heartbeatMessageId, TimeSpan.Zero, cancellationToken)
                .ConfigureAwait(false);

            var result = await MaterializeFinalTableAsync(context, statement, querySql, finalTable, cancellationToken)
                .WithHeartbeatAsync(
                context.Options.FinalTableWriteHeartbeatInterval,
                (elapsed, token) => context.Logger.TransformationWriteStartedAsync(heartbeatMessageId, elapsed, token),
                cancellationToken).ConfigureAwait(false);

            await context.Logger.TransformationRowsLoadedAsync(result.Value, result.Elapsed, heartbeatMessageId, cancellationToken)
                .ConfigureAwait(false);
            return result.Value;
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
        return await new ClickHouseFinalTableMaterializer().MaterializeAsync(
            context,
            querySql,
            finalTable,
            statement.IsMapped ? LoadClickHouseTableKind.Mapped : LoadClickHouseTableKind.Final,
            cancellationToken).ConfigureAwait(false);
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

    private FinalClickHouseTable CreateFinalTable(ScriptContext context)
    {
        var finalTable = CreatePhysicalFinalTableName(context);
        return new FinalClickHouseTable(
            finalTable,
            () => DropFinalTableBestEffortAsync(context, finalTable));
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
        string tableName,
        ResolvedQuery resolvedQuery,
        ClickHouseTableName finalTable,
        long rowCount)
    {
        return new LoadedTable
        {
            Name = finalTable,
            Alias = tableName,
            Kind = statement.Kind switch
            {
                LoadTableKind.Temp => LoadedTableKind.Temp,
                LoadTableKind.Mapped => LoadedTableKind.Mapped,
                _ => LoadedTableKind.Normal
            },
            RowCount = rowCount,
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

    private static LoadStatement ResolveLoadFieldAliases(LoadStatement statement)
    {
        if (statement.Fields is null)
        {
            return statement;
        }

        var fields = new List<LoadField>(statement.Fields.Count);
        foreach (var field in statement.Fields)
        {
            fields.Add(string.IsNullOrWhiteSpace(field.Name)
                ? field with
                {
                    Name = InferLoadFieldAlias(field.Expression),
                    Span = field.Expression.Span
                }
                : field);
        }

        var resolvedStatement = statement with
        {
            Fields = fields
        };
        ThrowIfDuplicateSelectAliases(resolvedStatement);
        return resolvedStatement;
    }

    private static string InferLoadFieldAlias(Expr expression)
    {
        if (expression.TryGetSingleReferencedName(out var name) && name is not null)
        {
            return name;
        }

        throw new QueryResolutionException(
            "Не указано имя поля. Напишите AS [Название].",
            expression.Span);
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
            if (aliases.Add(field.Name!))
            {
                continue;
            }

            throw new QueryResolutionException(
                $"LOAD select alias '{field.Name!}' is duplicated.",
                field.Span);
        }
    }

    private static string ValidateTableName(ScriptContext context, LoadStatement statement)
    {
        if (string.IsNullOrWhiteSpace(statement.TableName))
        {
            throw new QueryResolutionException(
                "У LOAD должно быть имя таблицы. Напишите имя перед LOAD: table_name: LOAD ...",
                statement.LoadSpan);
        }

        if (context.ContainsLoadedTable(statement.TableName))
        {
            throw new QueryResolutionException(
                $"Имя LOAD таблицы '{statement.TableName}' уже занято.",
                statement.TableNameSpan ?? statement.LoadSpan);
        }

        return statement.TableName;
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

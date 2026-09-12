using ClickHouse.Client.ADO;
using Loader.Core.Decorators;
using Loader.Core.Providers.ClickHouse;
using Loader.Core.Providers.Sql;
using Loader.Core.Sources;
using Loader.Core.Tasks;
using Loader.Core.Writers.ClickHouse;

namespace Loader.Script.Execution;

/// <summary>
/// Материализует результат уже скомпилированного LOAD query в физическую ClickHouse-таблицу.
/// Источник здесь всегда DWH-side SQL: схема проверяется через ClickHouse, таблица создается,
/// затем данные перегружаются server-side через <c>INSERT SELECT</c>.
/// </summary>
internal sealed class ClickHouseFinalTableMaterializer
{
    /// <summary>
    /// Создает final/mapped таблицу, записывает в нее результат <paramref name="querySql"/>
    /// и возвращает фактическое количество записей после контрольного <c>count()</c>.
    /// </summary>
    public async ValueTask<ClickHouseFinalTableMaterialization> MaterializeAsync(
        ScriptContext context,
        string querySql,
        ClickHouseTableName finalTable,
        LoadClickHouseTableKind kind,
        CancellationToken cancellationToken)
    {
        var source = new ConnectionStringSource
        {
            ConnectionString = context.TargetConnectionString
        };

        // 1. Получаем схему результата без чтения данных, чтобы создать физическую таблицу с нужными типами.
        var schemaProbeSql = ClickHouseFinalTableSql.SchemaProbe(querySql);
        await context.DebugSqlAsync("Проверяем схему финальной таблицы", schemaProbeSql, cancellationToken)
            .ConfigureAwait(false);
        await using var schemaReader = await new ClickHouseProvider()
            .OpenReaderAsync(
                source,
                new SqlTableConfig { Sql = schemaProbeSql },
                cancellationToken)
            .ConfigureAwait(false);
        await using var finalNameReader = schemaReader.AbstractColumns();
        await using var finalReader = finalNameReader.Normalize();

        // 2. Для обычных final tables одним aggregate-запросом собираем аналитику результата.
        // TEMP/MAPPED не получают пользовательскую аналитику и сохраняют старый путь без count() после insert.
        var analysis = kind == LoadClickHouseTableKind.Final
            ? await CollectAnalysisAsync(
                    context,
                    querySql,
                    finalReader.DataSchema,
                    finalNameReader.OriginalNames,
                    cancellationToken)
                .ConfigureAwait(false)
            : null;

        // 3. Создаем целевую таблицу под нормализованную схему результата.
        var writeOptions = CreateWriteOptions(finalTable, kind);
        var createSql = new ClickHouseWriter().BuildCreateTableSql(finalReader, writeOptions);
        await ExecuteNonQueryAsync(context, "Создаем финальную таблицу", createSql, cancellationToken).ConfigureAwait(false);

        // 4. Перегружаем данные внутри ClickHouse. Heartbeat относится только к этому долгому шагу.
        var insertSql = ClickHouseFinalTableSql.InsertSelect(finalTable, finalReader.DataSchema, querySql);
        var heartbeatMessageId = Guid.NewGuid().ToString("N");
        var insertResult = await ExecuteInsertSelectAsync(context, insertSql, heartbeatMessageId, cancellationToken)
            .ConfigureAwait(false);

        // 5. Row count нужен только обычным final tables. TEMP/MAPPED скрыты от пользователя и не оптимизируются.
        await context.Logger.TransformationRowsLoadedAsync(analysis?.RowCount, insertResult.Elapsed, heartbeatMessageId, cancellationToken)
            .ConfigureAwait(false);
        return new ClickHouseFinalTableMaterialization
        {
            RowCount = analysis?.RowCount,
            Columns = analysis?.Columns
        };
    }

    /// <summary>
    /// Выбирает настройки физической ClickHouse-таблицы для результата LOAD.
    /// </summary>
    private static ClickHouseWriteOptions CreateWriteOptions(
        ClickHouseTableName tableName,
        LoadClickHouseTableKind kind)
    {
        return new ClickHouseWriteOptions
        {
            TableName = tableName,
            Engine = kind switch
            {
                // Log запрещен в ClickHouse Cloud, поэтому используем минимальный MergeTree без ключа сортировки.
                LoadClickHouseTableKind.Temp => "MergeTree ORDER BY tuple()",

                // Полноценная стратегия ORDER BY для пользовательских final tables пока не определена.
                LoadClickHouseTableKind.Final => "MergeTree ORDER BY tuple()",

                // ApplyMap читает mapping через joinGetOrNull, поэтому нужна ClickHouse Join-таблица.
                LoadClickHouseTableKind.Mapped => "Join(ANY, LEFT, `column1`)",
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
            }
        };
    }

    /// <summary>
    /// Собирает table/column analysis по результату LOAD query одним aggregate-запросом.
    /// Эти данные пока не влияют на физическую таблицу, но будут использоваться для UI и будущих оптимизаций.
    /// </summary>
    private static async ValueTask<ClickHouseFinalTableMaterialization> CollectAnalysisAsync(
        ScriptContext context,
        string querySql,
        Loader.Core.Models.DataSchema schema,
        IReadOnlyList<string> sourceColumnNames,
        CancellationToken cancellationToken)
    {
        await using var connection = new ClickHouseConnection(context.TargetConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var sql = ClickHouseFinalTableAnalysisSql.Build(querySql, schema, sourceColumnNames);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await context.DebugSqlAsync("Запрос для анализа финальной таблицы", sql, cancellationToken)
            .ConfigureAwait(false);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return new ClickHouseFinalTableMaterialization
            {
                RowCount = 0,
                Columns = []
            };
        }

        var rowCount = ToInt64(reader.GetValue(0));
        var columns = new List<ClickHouseFinalColumnAnalysis>(schema.Fields.Count);
        var ordinal = 1;
        for (var fieldOrdinal = 0; fieldOrdinal < schema.Fields.Count; fieldOrdinal++)
        {
            var field = schema.Fields[fieldOrdinal];
            var nonNullCount = ToInt64OrZero(reader.GetValue(ordinal++));
            var distinctCount = ToInt64OrZero(reader.GetValue(ordinal++));
            var min = ToNullableValue(reader.GetValue(ordinal++));
            var max = ToNullableValue(reader.GetValue(ordinal++));
            columns.Add(new ClickHouseFinalColumnAnalysis
            {
                Ordinal = fieldOrdinal,
                NonNullCount = nonNullCount,
                ApproxDistinctNonNullCount = distinctCount,
                Min = min,
                Max = max
            });
        }

        return new ClickHouseFinalTableMaterialization
        {
            RowCount = rowCount,
            Columns = columns
        };
    }

    /// <summary>
    /// Выполняет <c>INSERT INTO final SELECT ...</c> и отправляет обновляемый progress heartbeat.
    /// Возвращает точное время именно вставки, без schema probe/create/count.
    /// </summary>
    private static async ValueTask<TaskHeartbeatResult> ExecuteInsertSelectAsync(
        ScriptContext context,
        string sql,
        string heartbeatMessageId,
        CancellationToken cancellationToken)
    {
        await using var connection = new ClickHouseConnection(context.TargetConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await context.DebugSqlAsync("Перегружаем данные в финальную таблицу", sql, cancellationToken)
            .ConfigureAwait(false);

        await context.Logger.TransformationWriteStartedAsync(heartbeatMessageId, TimeSpan.Zero, cancellationToken)
            .ConfigureAwait(false);
        var result = await command.ExecuteNonQueryAsync(cancellationToken)
            .WithHeartbeatAsync(
                context.Options.FinalTableWriteHeartbeatInterval,
                (elapsed, token) => context.Logger.TransformationWriteStartedAsync(heartbeatMessageId, elapsed, token),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Logger.TransformationDataLoadedAsync(result.Elapsed, heartbeatMessageId, cancellationToken)
            .ConfigureAwait(false);

        return new TaskHeartbeatResult(result.Elapsed);
    }

    private static long ToInt64(object value)
    {
        return Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static long ToInt64OrZero(object value)
    {
        return value == DBNull.Value
            ? 0
            : ToInt64(value);
    }

    private static object? ToNullableValue(object value)
    {
        return value == DBNull.Value ? null : value;
    }

    /// <summary>
    /// Выполняет простой ClickHouse statement без результата и пишет debug SQL перед выполнением.
    /// </summary>
    private static async ValueTask ExecuteNonQueryAsync(
        ScriptContext context,
        string title,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var connection = new ClickHouseConnection(context.TargetConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await context.DebugSqlAsync(title, sql, cancellationToken)
            .ConfigureAwait(false);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

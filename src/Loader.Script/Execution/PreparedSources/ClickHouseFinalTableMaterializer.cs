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
    public async ValueTask<long> MaterializeAsync(
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

        // 2. Создаем целевую таблицу под нормализованную схему результата.
        var writeOptions = CreateWriteOptions(finalTable, kind);
        var createSql = new ClickHouseWriter().BuildCreateTableSql(finalReader, writeOptions);
        await ExecuteNonQueryAsync(context, "Создаем финальную таблицу", createSql, cancellationToken).ConfigureAwait(false);

        // 3. Перегружаем данные внутри ClickHouse. Heartbeat относится только к этому долгому шагу.
        var insertSql = ClickHouseFinalTableSql.InsertSelect(finalTable, finalReader.DataSchema, querySql);
        var heartbeatMessageId = Guid.NewGuid().ToString("N");
        var insertResult = await ExecuteInsertSelectAsync(context, insertSql, heartbeatMessageId, cancellationToken)
            .ConfigureAwait(false);

        // 4. После записи считаем строки отдельным запросом и обновляем тот же progress message.
        var rowCount = await CountRowsAsync(context, finalTable, cancellationToken).ConfigureAwait(false);
        await context.Logger.TransformationRowsLoadedAsync(rowCount, insertResult.Elapsed, heartbeatMessageId, cancellationToken)
            .ConfigureAwait(false);
        return rowCount;
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
    /// Считает фактическое количество строк уже созданной final table.
    /// </summary>
    private static async ValueTask<long> CountRowsAsync(
        ScriptContext context,
        ClickHouseTableName table,
        CancellationToken cancellationToken)
    {
        await using var connection = new ClickHouseConnection(context.TargetConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = ClickHouseFinalTableSql.CountRows(table);
        await context.DebugSqlAsync("Считаем записи финальной таблицы", command.CommandText, cancellationToken)
            .ConfigureAwait(false);
        var count = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(count, System.Globalization.CultureInfo.InvariantCulture);
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

namespace Loader.Script;

public static class ProgressLoggerExtensions
{
    public static ValueTask LoadTableStartedAsync(
        this IProgressLogger logger,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        return logger.ReportAsync(new ScriptProgressEvent
        {
            Kind = "LoadTableStarted",
            Message = $"Загружается таблица [{tableName}]"
        }, cancellationToken);
    }

    public static ValueTask FileSourceReadStartedAsync(
        this IProgressLogger logger,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        return logger.ReportAsync(new ScriptProgressEvent
        {
            Kind = "FileSourceReadStarted",
            Message = $"Выгружаем данные из файла '{fileName}'"
        }, cancellationToken);
    }

    public static ValueTask ConnectionOpeningAsync(
        this IProgressLogger logger,
        string connectionName,
        CancellationToken cancellationToken = default)
    {
        return logger.ReportAsync(new ScriptProgressEvent
        {
            Kind = "ConnectionOpening",
            Message = $"Открываем подключение к '{connectionName}'"
        }, cancellationToken);
    }

    public static ValueTask SqlSourceReadStartedAsync(
        this IProgressLogger logger,
        CancellationToken cancellationToken = default)
    {
        return logger.ReportAsync(new ScriptProgressEvent
        {
            Kind = "SqlSourceReadStarted",
            Message = "Выгружаем данные по запросу SQL"
        }, cancellationToken);
    }

    public static ValueTask SourceRowsLoadedAsync(
        this IProgressLogger logger,
        long rowCount,
        CancellationToken cancellationToken = default)
    {
        return logger.SourceRowsLoadedAsync(rowCount, messageId: null, elapsed: null, completed: true, cancellationToken);
    }

    public static ValueTask SourceRowsLoadedAsync(
        this IProgressLogger logger,
        long rowCount,
        string? messageId,
        TimeSpan? elapsed = null,
        bool completed = true,
        CancellationToken cancellationToken = default)
    {
        var message = completed
            ? $"Выгружено {rowCount} записей"
            : $"Выгружаем {rowCount} записей";
        if (elapsed is not null)
        {
            message += completed
                ? $". Заняло {FormatSeconds(elapsed.Value)} секунд."
                : $". Прошло {FormatSeconds(elapsed.Value)} секунд.";
        }

        return logger.ReportAsync(new ScriptProgressEvent
        {
            MessageId = messageId,
            Kind = "SourceRowsLoaded",
            Message = message
        }, cancellationToken);
    }

    public static ValueTask TransformationWriteStartedAsync(
        this IProgressLogger logger,
        string? messageId = null,
        TimeSpan? elapsed = null,
        CancellationToken cancellationToken = default)
    {
        var message = elapsed is null
            ? "Загружаем данные после трансформаций"
            : $"Загружаем таблицу. Прошло {FormatSeconds(elapsed.Value)} секунд.";

        return logger.ReportAsync(new ScriptProgressEvent
        {
            MessageId = messageId,
            Kind = "TransformationWriteStarted",
            Message = message
        }, cancellationToken);
    }

    public static ValueTask TransformationRowsLoadedAsync(
        this IProgressLogger logger,
        long? rowCount,
        TimeSpan? elapsed = null,
        string? messageId = null,
        CancellationToken cancellationToken = default)
    {
        var message = rowCount is null
            ? elapsed is null
                ? "Данные загружены"
                : $"Данные загружены за {FormatSeconds(elapsed.Value)} секунд."
            : elapsed is null
                ? $"Загружено {rowCount.Value} записей"
                : $"Загружено {rowCount.Value} записей за {FormatSeconds(elapsed.Value)} секунд.";

        return logger.ReportAsync(new ScriptProgressEvent
        {
            MessageId = messageId,
            Kind = "TransformationRowsLoaded",
            Message = message
        }, cancellationToken);
    }

    public static ValueTask TransformationDataLoadedAsync(
        this IProgressLogger logger,
        TimeSpan elapsed,
        string? messageId = null,
        CancellationToken cancellationToken = default)
    {
        return logger.ReportAsync(new ScriptProgressEvent
        {
            MessageId = messageId,
            Kind = "TransformationDataLoaded",
            Message = $"Данные загружены за {FormatSeconds(elapsed)} секунд."
        }, cancellationToken);
    }

    private static string FormatSeconds(TimeSpan elapsed)
    {
        return elapsed.TotalSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }

    public static ValueTask DropTableStartedAsync(
        this IProgressLogger logger,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        return logger.ReportAsync(new ScriptProgressEvent
        {
            Kind = "DropTableStarted",
            Message = $"Удаляем таблицу [{tableName}]"
        }, cancellationToken);
    }

    public static ValueTask TempLoadCleanupStartedAsync(
        this IProgressLogger logger,
        CancellationToken cancellationToken = default)
    {
        return logger.ReportAsync(new ScriptProgressEvent
        {
            Kind = "TempLoadCleanupStarted",
            Message = "Чистим TEMP LOAD таблицы"
        }, cancellationToken);
    }

    public static ValueTask DebugSqlAsync(
        this ScriptContext context,
        string title,
        string sql,
        CancellationToken cancellationToken = default)
    {
        return context.Options.EmitDebugProgress
            ? context.Logger.DebugSqlAsync(title, sql, cancellationToken)
            : ValueTask.CompletedTask;
    }

    public static ValueTask DebugSqlAsync(
        this IProgressLogger logger,
        string title,
        string sql,
        CancellationToken cancellationToken = default)
    {
        return logger.ReportAsync(new ScriptProgressEvent
        {
            Kind = "DebugSql",
            Level = ScriptProgressLevel.Debug,
            Message = title,
            DebugPayload = sql
        }, cancellationToken);
    }
}

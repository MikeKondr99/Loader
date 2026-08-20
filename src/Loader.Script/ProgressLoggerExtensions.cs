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
        return logger.ReportAsync(new ScriptProgressEvent
        {
            Kind = "SourceRowsLoaded",
            Message = $"Было загружено {rowCount} записей"
        }, cancellationToken);
    }

    public static ValueTask TransformationWriteStartedAsync(
        this IProgressLogger logger,
        CancellationToken cancellationToken = default)
    {
        return logger.ReportAsync(new ScriptProgressEvent
        {
            Kind = "TransformationWriteStarted",
            Message = "Загружаем данные после трансформаций"
        }, cancellationToken);
    }

    public static ValueTask TransformationRowsLoadedAsync(
        this IProgressLogger logger,
        long rowCount,
        CancellationToken cancellationToken = default)
    {
        return logger.ReportAsync(new ScriptProgressEvent
        {
            Kind = "TransformationRowsLoaded",
            Message = $"Было загружено {rowCount} записей"
        }, cancellationToken);
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
        this IProgressLogger logger,
        string sql,
        CancellationToken cancellationToken = default)
    {
        return logger.ReportAsync(new ScriptProgressEvent
        {
            Kind = "DebugSql",
            Level = ScriptProgressLevel.Debug,
            Message = sql
        }, cancellationToken);
    }
}

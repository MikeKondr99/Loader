using Microsoft.Extensions.Logging;

namespace Loader.Script;

public static partial class ScriptLog
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Определяю provider для LOAD source '{Source}'.")]
    public static partial void ResolvingLoadProvider(this ILogger logger, string source);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Provider определен: {ProviderKind}.")]
    public static partial void LoadProviderResolved(this ILogger logger, string providerKind);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "Открываю reader для LOAD source '{Source}'.")]
    public static partial void OpeningLoadReader(this ILogger logger, string source);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "Загружаю LOAD source во временную таблицу '{TempTable}'.")]
    public static partial void LoadingTempTable(this ILogger logger, string tempTable);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Information,
        Message = "Временная таблица '{TempTable}' загружена.")]
    public static partial void TempTableLoaded(this ILogger logger, string tempTable);
}

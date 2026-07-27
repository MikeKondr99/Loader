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

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Information,
        Message = "Материализую результат LOAD во финальную таблицу '{FinalTable}'.")]
    public static partial void MaterializingFinalTable(this ILogger logger, string finalTable);

    [LoggerMessage(
        EventId = 1006,
        Level = LogLevel.Information,
        Message = "Финальная таблица '{FinalTable}' материализована.")]
    public static partial void FinalTableMaterialized(this ILogger logger, string finalTable);

    [LoggerMessage(
        EventId = 1007,
        Level = LogLevel.Information,
        Message = "Удаляю временную таблицу '{TempTable}'.")]
    public static partial void DroppingTempTable(this ILogger logger, string tempTable);

    [LoggerMessage(
        EventId = 1008,
        Level = LogLevel.Information,
        Message = "Временная таблица '{TempTable}' удалена.")]
    public static partial void TempTableDropped(this ILogger logger, string tempTable);

    [LoggerMessage(
        EventId = 1009,
        Level = LogLevel.Warning,
        Message = "Не удалось удалить временную таблицу '{TempTable}'.")]
    public static partial void TempTableDropFailed(this ILogger logger, string tempTable, Exception exception);

    [LoggerMessage(
        EventId = 1010,
        Level = LogLevel.Information,
        Message = "Удаляю неуспешную финальную таблицу '{FinalTable}'.")]
    public static partial void DroppingFinalTable(this ILogger logger, string finalTable);

    [LoggerMessage(
        EventId = 1011,
        Level = LogLevel.Information,
        Message = "Неуспешная финальная таблица '{FinalTable}' удалена.")]
    public static partial void FinalTableDropped(this ILogger logger, string finalTable);

    [LoggerMessage(
        EventId = 1012,
        Level = LogLevel.Warning,
        Message = "Не удалось удалить неуспешную финальную таблицу '{FinalTable}'.")]
    public static partial void FinalTableDropFailed(this ILogger logger, string finalTable, Exception exception);
}

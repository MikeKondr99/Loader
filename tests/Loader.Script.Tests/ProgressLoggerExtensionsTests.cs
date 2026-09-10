namespace Loader.Script.Tests;

public sealed class ProgressLoggerExtensionsTests
{
    [Test]
    public async Task Progress_extensions_emit_user_messages()
    {
        var logger = new TestProgressLogger();

        await logger.LoadTableStartedAsync("iris");
        await logger.FileSourceReadStartedAsync("file.csv");
        await logger.SourceRowsLoadedAsync(150);
        await logger.TransformationWriteStartedAsync();
        await logger.TransformationRowsLoadedAsync(140);

        await Assert.That(logger.Events.Select(static item => item.Kind).ToArray())
            .IsEquivalentTo(
                [
                    "LoadTableStarted",
                    "FileSourceReadStarted",
                    "SourceRowsLoaded",
                    "TransformationWriteStarted",
                    "TransformationRowsLoaded"
                ],
                TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(logger.Events.Select(static item => item.Message).ToArray())
            .IsEquivalentTo(
                [
                    "Загружается таблица [iris]",
                    "Выгружаем данные из файла 'file.csv'",
                    "Выгружено 150 записей",
                    "Загружаем данные после трансформаций",
                    "Загружено 140 записей"
                ],
                TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(logger.Events.All(static item => item.Level == ScriptProgressLevel.User)).IsTrue();
        await Assert.That(logger.Events.All(static item => item.MessageId is null)).IsTrue();
    }

    [Test]
    public async Task Source_rows_loaded_can_emit_update_message_id()
    {
        var logger = new TestProgressLogger();

        await logger.SourceRowsLoadedAsync(150, "source-progress", TimeSpan.FromSeconds(12.341), completed: false);

        await Assert.That(logger.Events[0].Kind).IsEqualTo("SourceRowsLoaded");
        await Assert.That(logger.Events[0].MessageId).IsEqualTo("source-progress");
        await Assert.That(logger.Events[0].Message).IsEqualTo("Выгружаем 150 записей. Прошло 12.341 секунд.");
    }

    [Test]
    public async Task Progress_extensions_emit_connection_sql_and_debug_messages()
    {
        var logger = new TestProgressLogger();

        await logger.LoadTableStartedAsync("заказы");
        await logger.ConnectionOpeningAsync("connect_name");
        await logger.SqlSourceReadStartedAsync();
        await logger.DebugSqlAsync("SELECT 1");

        await Assert.That(logger.Events.Select(static item => item.Message).ToArray())
            .IsEquivalentTo(
                [
                    "Загружается таблица [заказы]",
                    "Открываем подключение к 'connect_name'",
                    "Выгружаем данные по запросу SQL",
                    "SELECT 1"
                ],
                TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(logger.Events[^1].Level).IsEqualTo(ScriptProgressLevel.Debug);
    }

    [Test]
    public async Task Progress_extensions_emit_drop_and_temp_cleanup_messages()
    {
        var logger = new TestProgressLogger();

        await logger.DropTableStartedAsync("old_table");
        await logger.TempLoadCleanupStartedAsync();

        await Assert.That(logger.Events.Select(static item => item.Kind).ToArray())
            .IsEquivalentTo(
                [
                    "DropTableStarted",
                    "TempLoadCleanupStarted"
                ],
                TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(logger.Events.Select(static item => item.Message).ToArray())
            .IsEquivalentTo(
                [
                    "Удаляем таблицу [old_table]",
                    "Чистим TEMP LOAD таблицы"
                ],
                TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(logger.Events.All(static item => item.Level == ScriptProgressLevel.User)).IsTrue();
    }

    private sealed class TestProgressLogger : IProgressLogger
    {
        public List<ScriptProgressEvent> Events { get; } = [];

        public ValueTask ReportAsync(ScriptProgressEvent progressEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(progressEvent);
            return ValueTask.CompletedTask;
        }
    }
}

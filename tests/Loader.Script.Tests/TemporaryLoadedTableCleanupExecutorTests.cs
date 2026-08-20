using Loader.Core.Sources;
using Loader.Core.Writers.ClickHouse;
using Loader.Script.Execution;

namespace Loader.Script.Tests;

public sealed class TemporaryLoadedTableCleanupExecutorTests
{
    [Test]
    [DisplayName("TEMP LOAD cleanup пишет progress для общего cleanup и каждой таблицы")]
    public async Task Execute_reports_temp_cleanup_progress_events()
    {
        var logger = new RecordingProgressLogger();
        var executor = new RecordingTemporaryLoadedTableCleanupExecutor();
        var context = CreateContext(logger);
        context.AddLoadedTable(LoadedTable("temp_raw_physical", "raw", LoadedTableKind.Temp));
        context.AddLoadedTable(LoadedTable("final_physical", "final", LoadedTableKind.Normal));

        await executor.ExecuteAsync(context);

        await Assert.That(executor.DroppedTables.Select(static table => table.Table).ToArray())
            .IsEquivalentTo(["temp_raw_physical"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(context.LoadedTables.Select(static table => table.Alias!).ToArray())
            .IsEquivalentTo(["final"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(logger.Events.Select(static item => item.Kind).ToArray())
            .IsEquivalentTo(
                [
                    "TempLoadCleanupStarted",
                    "DropTableStarted"
                ],
                TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(logger.Events.Select(static item => item.Message).ToArray())
            .IsEquivalentTo(
                [
                    "Чистим TEMP LOAD таблицы",
                    "Удаляем таблицу [raw]"
                ],
                TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("TEMP LOAD cleanup не пишет progress если временных таблиц нет")]
    public async Task Execute_does_not_report_progress_when_there_are_no_temp_tables()
    {
        var logger = new RecordingProgressLogger();
        var executor = new RecordingTemporaryLoadedTableCleanupExecutor();
        var context = CreateContext(logger);
        context.AddLoadedTable(LoadedTable("final_physical", "final", LoadedTableKind.Normal));

        await executor.ExecuteAsync(context);

        await Assert.That(executor.DroppedTables).IsEmpty();
        await Assert.That(logger.Events).IsEmpty();
        await Assert.That(context.LoadedTables).Count().IsEqualTo(1);
    }

    private static LoadedTable LoadedTable(string physicalName, string alias, LoadedTableKind kind)
    {
        return new LoadedTable
        {
            Name = new ClickHouseTableName
            {
                Table = physicalName
            },
            Alias = alias,
            Kind = kind,
            Fields = []
        };
    }

    private static ScriptContext CreateContext(IProgressLogger logger)
    {
        return new ScriptContext
        {
            FileStorage = new StubFileSource(),
            TargetConnectionString = "Host=localhost",
            Logger = logger
        };
    }

    private sealed class RecordingTemporaryLoadedTableCleanupExecutor : TemporaryLoadedTableCleanupExecutor
    {
        public List<ClickHouseTableName> DroppedTables { get; } = [];

        protected override ValueTask DropTableAsync(
            ScriptContext context,
            ClickHouseTableName tableName,
            CancellationToken cancellationToken)
        {
            DroppedTables.Add(tableName);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingProgressLogger : IProgressLogger
    {
        public List<ScriptProgressEvent> Events { get; } = [];

        public ValueTask ReportAsync(ScriptProgressEvent progressEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(progressEvent);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubFileSource : IFileSource
    {
        public Stream OpenRead(string fileName)
        {
            return new MemoryStream();
        }
    }
}

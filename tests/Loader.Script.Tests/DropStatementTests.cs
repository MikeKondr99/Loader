using Loader.Core.Sources;
using Loader.Core.Writers.ClickHouse;
using Loader.Lang.Statements;
using Loader.Script.Execution;

namespace Loader.Script.Tests;

public sealed class DropStatementTests
{
    [Test]
    [DisplayName("Script DROP удаляет physical final table и убирает alias из результата")]
    public async Task Execute_script_drop_removes_physical_table_and_context_entry()
    {
        var executor = CreateExecutor(out var dropExecutor);
        var logger = new RecordingProgressLogger();

        var tables = await executor.ExecuteAsync(
            CreateContext(logger),
            Parse(
                """
                orders: LOAD * FROM Csv(path='orders.csv');
                DROP orders;
                """));

        await Assert.That(dropExecutor.DropCalls).IsEqualTo(1);
        await Assert.That(dropExecutor.DroppedTableName!.Table).IsEqualTo("physical_orders");
        await Assert.That(logger.Events.Select(static item => item.Kind).ToArray())
            .Contains("DropTableStarted");
        await Assert.That(logger.Events.Single(static item => item.Kind == "DropTableStarted").Message)
            .IsEqualTo("Удаляем таблицу [orders]");
        await Assert.That(tables).IsEmpty();
    }

    [Test]
    [DisplayName("Script не чистит TEMP LOAD повторно если его уже удалил DROP")]
    public async Task Execute_script_drop_temp_load_prevents_second_cleanup_drop()
    {
        var cleanupExecutor = new RecordingTemporaryCleanupExecutor();
        var executor = new ScriptExecutor
        {
            LoadStatementExecutor = new NoopLoadStatementExecutor(),
            DropStatementExecutor = new TestDropStatementExecutor(),
            TemporaryTableCleanupExecutor = cleanupExecutor
        };
        var dropExecutor = (TestDropStatementExecutor)executor.DropStatementExecutor;

        var tables = await executor.ExecuteAsync(
            CreateContext(),
            Parse(
                """
                orders:
                TEMP LOAD * FROM Csv(path='orders.csv');
                DROP orders;
                """));

        await Assert.That(dropExecutor.DropCalls).IsEqualTo(1);
        await Assert.That(dropExecutor.DroppedTableName!.Table).IsEqualTo("physical_orders");
        await Assert.That(cleanupExecutor.ExecuteCalls).IsEqualTo(1);
        await Assert.That(cleanupExecutor.CleanedAliases).IsEmpty();
        await Assert.That(tables).IsEmpty();
    }

    [Test]
    [DisplayName("Script DROP неизвестной таблицы оборачивается с номером statement и span имени")]
    public async Task Execute_script_drop_unknown_alias_throws_script_exception()
    {
        var executor = CreateExecutor(out var dropExecutor);
        var script = Parse("DROP missing;");
        var statement = (DropStatement)script.Statements[0];

        var exception = await Assert.That(async () => await executor.ExecuteAsync(CreateContext(), script))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(exception!.StatementIndex).IsEqualTo(0);
        await Assert.That(exception.StatementType).IsEqualTo(nameof(DropStatement));
        await Assert.That(exception.Stage).IsEqualTo(LoadScriptStage.DropTable);
        await Assert.That(exception.Span).IsEqualTo(statement.NameSpan);
        await Assert.That(exception.Message).Contains(nameof(LoadScriptStage.DropTable));
        await Assert.That(dropExecutor.DropCalls).IsEqualTo(0);
    }

    [Test]
    [DisplayName("Script DROP при ошибке ClickHouse не удаляет alias из ScriptContext")]
    public async Task Execute_script_drop_failure_keeps_context_entry()
    {
        var executor = CreateExecutor(out var dropExecutor);
        dropExecutor.ThrowOnDrop = true;
        var context = CreateContext();

        var exception = await Assert.That(async () => await executor.ExecuteAsync(
                context,
                Parse(
                    """
                    orders: LOAD * FROM Csv(path='orders.csv');
                    DROP orders;
                    """)))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(exception!.StatementIndex).IsEqualTo(1);
        await Assert.That(exception.Stage).IsEqualTo(LoadScriptStage.DropTable);
        await Assert.That(exception.InnerException).IsTypeOf<LoadScriptExecutionException>();
        await Assert.That(exception.InnerException!.InnerException).IsTypeOf<InvalidOperationException>();
        await Assert.That(context.LoadedTables).Count().IsEqualTo(1);
        await Assert.That(context.LoadedTables[0].Alias).IsEqualTo("orders");
    }

    private static ScriptExecutor CreateExecutor(out TestDropStatementExecutor dropExecutor)
    {
        dropExecutor = new TestDropStatementExecutor();
        return new ScriptExecutor
        {
            LoadStatementExecutor = new NoopLoadStatementExecutor(),
            DropStatementExecutor = dropExecutor
        };
    }

    private static Loader.Lang.Script Parse(string text)
    {
        return Loader.Lang.Script.Parse(text).Value!;
    }

    private static LoadedTable LoadedTable(string physicalName, string alias)
    {
        return new LoadedTable
        {
            Name = new ClickHouseTableName
            {
                Table = physicalName
            },
            Alias = alias,
            Fields = []
        };
    }

    private static ScriptContext CreateContext(IProgressLogger? logger = null)
    {
        return new ScriptContext
        {
            FileStorage = new StubFileSource(),
            TargetConnectionString = "Host=localhost",
            Logger = logger ?? NullProgressLogger.Instance
        };
    }

    private sealed class TestDropStatementExecutor : DropStatementExecutor
    {
        public int DropCalls { get; private set; }

        public ClickHouseTableName? DroppedTableName { get; private set; }

        public bool ThrowOnDrop { get; set; }

        protected override ValueTask DropTableAsync(
            ScriptContext context,
            ClickHouseTableName tableName,
            CancellationToken cancellationToken)
        {
            DropCalls++;
            DroppedTableName = tableName;
            if (ThrowOnDrop)
            {
                throw new InvalidOperationException("drop failed");
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class NoopLoadStatementExecutor : LoadStatementExecutor
    {
        public override ValueTask<LoadedTable> ExecuteAsync(
            ScriptContext context,
            LoadStatement statement,
            CancellationToken cancellationToken = default)
        {
            var table = LoadedTable("physical_orders", statement.TableName) with
            {
                Kind = statement.IsTemporary ? LoadedTableKind.Temp : LoadedTableKind.Normal
            };
            context.AddLoadedTable(table);
            return ValueTask.FromResult(table);
        }
    }

    private sealed class RecordingTemporaryCleanupExecutor : TemporaryLoadedTableCleanupExecutor
    {
        public int ExecuteCalls { get; private set; }

        public List<string> CleanedAliases { get; } = [];

        public override ValueTask ExecuteAsync(
            ScriptContext context,
            CancellationToken cancellationToken = default)
        {
            ExecuteCalls++;
            foreach (var table in context.LoadedTables
                         .Where(static table => table.Kind == LoadedTableKind.Temp)
                         .ToArray())
            {
                CleanedAliases.Add(table.Alias!);
                context.RemoveLoadedTable(table);
            }

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

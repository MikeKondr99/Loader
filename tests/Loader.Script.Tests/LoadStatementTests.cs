using System.Data;
using System.Data.Common;
using Loader.Core.Decorators;
using Loader.Core.Exceptions;
using Loader.Core.Sources;
using Loader.Core.Writers.ClickHouse;
using Loader.Lang;
using Loader.Lang.Expressions;
using Loader.Lang.Statements;
using Loader.Script.Execution;

namespace Loader.Script.Tests;

public sealed class LoadStatementTests
{
    [Test]
    public async Task Load_temp_table_resolves_source_normalizes_physical_columns_and_writes_temp_table()
    {
        var providerResolver = new FakeProviderResolver();
        var executor = new TestLoadStatementExecutor
        {
            ProviderResolver = providerResolver
        };
        var statement = new LoadStatement
        {
            TableName = "orders",
            LoadSpan = Span(),
            Fields = null,
            FromSpan = Span(),
            SourceCall = SourceCall("Csv", "orders.csv"),
            Where = null,
            GroupBy = null,
            OrderBy = null
        };

        await using var result = await executor.LoadTempTableAsync(CreateContext(), statement);

        await Assert.That(providerResolver.ResolveCalls).IsEqualTo(1);
        await Assert.That(executor.WriteCalls).IsEqualTo(1);
        await Assert.That(result.TableName.Table).StartsWith("tmp_");
        await Assert.That(result.TableName.Table).DoesNotContain("orders");
        await Assert.That(result.OriginalColumnNames).Count().IsEqualTo(2);
        await Assert.That(result.OriginalColumnNames[0]).IsEqualTo("id");
        await Assert.That(result.OriginalColumnNames[1]).IsEqualTo("name");
        await Assert.That(result.Schema.Fields[0].Name).IsEqualTo("column1");
        await Assert.That(result.Schema.Fields[1].Name).IsEqualTo("column2");
        await Assert.That(executor.TableName!.Table).IsEqualTo(result.TableName.Table);
        await Assert.That(executor.Rows).Count().IsEqualTo(1);
        await Assert.That(executor.Rows[0][0]).IsEqualTo(1);
        await Assert.That(executor.Rows[0][1]).IsEqualTo("Moscow");
    }

    [Test]
    public async Task Execute_load_writes_temp_table_materializes_final_table_and_registers_loaded_table()
    {
        var providerResolver = new FakeProviderResolver();
        var executor = new TestLoadStatementExecutor
        {
            ProviderResolver = providerResolver
        };
        var context = CreateContext();
        var statement = new LoadStatement
        {
            TableName = "orders",
            Fields =
            [
                new LoadField
                {
                    Name = "city",
                    Span = Span(),
                    Expression = Expr.Parse("name").Value
                }
            ],
            FromSpan = Span(),
            SourceCall = SourceCall("Csv", "orders.csv"),
            Where = Expr.Parse("id > 0").Value,
            GroupBy = null,
            OrderBy =
            [
                new LoadOrderField
                {
                    Expression = Expr.Parse("name").Value,
                    Direction = LoadOrderDirection.Ascending
                }
            ],
            LimitPart = new LimitPart
            {
                Value = 10,
                Span = Span()
            },
            Offset = 1
        };

        var loadedTable = await executor.ExecuteAsync(context, statement);

        await Assert.That(executor.WriteCalls).IsEqualTo(1);
        await Assert.That(executor.MaterializeCalls).IsEqualTo(1);
        await Assert.That(executor.DropCalls).IsEqualTo(1);
        await Assert.That(executor.DropTableName!.Table).IsEqualTo(executor.TableName!.Table);
        await Assert.That(executor.DropFinalCalls).IsEqualTo(0);
        await Assert.That(executor.FinalTableName!.Table).StartsWith("final_");
        await Assert.That(executor.FinalTableName!.Table).DoesNotContain("orders");
        await Assert.That(executor.QuerySql).Contains("stage.`column2` AS `city`");
        await Assert.That(executor.QuerySql).Contains("WHERE (stage.`column1` > 0)");
        await Assert.That(executor.QuerySql).Contains("ORDER BY stage.`column2` ASC");
        await Assert.That(executor.QuerySql).Contains("LIMIT 10");
        await Assert.That(executor.QuerySql).Contains("OFFSET 1");
        await Assert.That(loadedTable.Name).IsSameReferenceAs(executor.FinalTableName);
        await Assert.That(loadedTable.Alias).IsEqualTo("orders");
        await Assert.That(loadedTable.Fields).Count().IsEqualTo(1);
        await Assert.That(loadedTable.Fields[0].Name).IsEqualTo("city");
        await Assert.That(context.LoadedTables).Count().IsEqualTo(1);
        await Assert.That(context.LoadedTables[0]).IsSameReferenceAs(loadedTable);
    }

    [Test]
    [DisplayName("Execute LOAD отправляет progress с фактическим количеством строк")]
    public async Task Execute_load_reports_progress_row_counts()
    {
        var logger = new RecordingProgressLogger();
        var executor = new TestLoadStatementExecutor
        {
            ProviderResolver = new FakeProviderResolver
            {
                SourceKind = "csv"
            },
            FinalRowCount = 1
        };
        var context = CreateContext(logger: logger);
        var statement = new LoadStatement
        {
            TableName = "orders",
            LoadSpan = Span(),
            Fields =
            [
                new LoadField
                {
                    Name = "city",
                    Span = Span(),
                    Expression = Expr.Parse("name").Value
                }
            ],
            FromSpan = Span(),
            SourceCall = SourceCall("Csv", "orders.csv"),
            Where = null,
            GroupBy = null,
            OrderBy = null
        };

        await executor.ExecuteAsync(context, statement);

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
        await Assert.That(logger.Events.Single(static item => item.Kind == "SourceRowsLoaded").Message)
            .Contains("1");
        await Assert.That(logger.Events.Single(static item => item.Kind == "TransformationRowsLoaded").Message)
            .Contains("1");
    }

    [Test]
    [DisplayName("Execute LOAD читает Numbers provider и строит query поверх generated number field")]
    public async Task Execute_load_reads_numbers_provider()
    {
        var executor = new TestLoadStatementExecutor
        {
            ProviderResolver = new LoadProviderResolver()
        };
        var context = CreateContext();
        var script = Loader.Lang.Script.Parse(
            """
            numbers:
            LOAD
                number AS value
            FROM Numbers(max=3);
            """).Value!;
        var statement = (LoadStatement)script.Statements[0];

        var loadedTable = await executor.ExecuteAsync(context, statement);

        await Assert.That(executor.WriteCalls).IsEqualTo(1);
        await Assert.That(executor.Rows.Select(static row => (long)row[0]).ToArray())
            .IsEquivalentTo([0L, 1L, 2L, 3L], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(executor.QuerySql).Contains("stage.`column1` AS `value`");
        await Assert.That(loadedTable.Alias).IsEqualTo("numbers");
        await Assert.That(loadedTable.Fields).Count().IsEqualTo(1);
        await Assert.That(loadedTable.Fields[0].Name).IsEqualTo("value");
    }

    [Test]
    [DisplayName("Execute LOAD FIRST ограничивает исходные строки до temp table")]
    public async Task Execute_load_first_limits_source_rows_before_temp_table()
    {
        var executor = new TestLoadStatementExecutor
        {
            ProviderResolver = new LoadProviderResolver()
        };
        var context = CreateContext();
        var script = Loader.Lang.Script.Parse(
            """
            numbers:
            FIRST 3
            LOAD
                number AS value
            FROM Numbers(max=10);
            """).Value!;
        var statement = (LoadStatement)script.Statements[0];

        await executor.ExecuteAsync(context, statement);

        await Assert.That(statement.First).IsEqualTo(3);
        await Assert.That(executor.Rows.Select(static row => (long)row[0]).ToArray())
            .IsEquivalentTo([0L, 1L, 2L], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(executor.QuerySql).DoesNotContain("LIMIT 3");
    }

    [Test]
    public async Task Execute_load_drops_temp_table_when_final_materialization_fails()
    {
        var executor = new TestLoadStatementExecutor
        {
            ProviderResolver = new FakeProviderResolver(),
            ThrowOnMaterialize = true
        };
        var context = CreateContext();
        var statement = new LoadStatement
        {
            TableName = "orders",
            LoadSpan = Span(),
            Fields = null,
            FromSpan = Span(),
            SourceCall = SourceCall("Csv", "orders.csv"),
            Where = null,
            GroupBy = null,
            OrderBy = null
        };

        var exception = await Assert.That(async () => await executor.ExecuteAsync(context, statement))
            .ThrowsExactly<LoadScriptExecutionException>();

        await Assert.That(exception!.Stage).IsEqualTo(LoadScriptStage.FinalTableWrite);
        await Assert.That(exception.Span).IsEqualTo(statement.LoadSpan);
        await Assert.That(exception.InnerException).IsTypeOf<InvalidOperationException>();
        await Assert.That(exception.InnerException!.Message).IsEqualTo("materialize failed");

        await Assert.That(executor.WriteCalls).IsEqualTo(1);
        await Assert.That(executor.MaterializeCalls).IsEqualTo(1);
        await Assert.That(executor.DropCalls).IsEqualTo(1);
        await Assert.That(executor.DropTableName!.Table).IsEqualTo(executor.TableName!.Table);
        await Assert.That(executor.DropFinalCalls).IsEqualTo(1);
        await Assert.That(executor.DropFinalTableName!.Table).IsEqualTo(executor.FinalTableName!.Table);
        await Assert.That(context.LoadedTables).IsEmpty();
    }

    [Test]
    public async Task Execute_load_wraps_final_materialization_errors_as_script_exception()
    {
        var executor = new TestLoadStatementExecutor
        {
            ProviderResolver = new FakeProviderResolver(),
            ThrowOnMaterialize = true
        };
        var context = CreateContext();
        var script = Loader.Lang.Script.Parse(
            """
            orders: LOAD *
            FROM Csv(path='orders.csv');
            """).Value!;
        var statement = (LoadStatement)script.Statements[0];

        var exception = await Assert.That(async () => await new ScriptExecutor
            {
                LoadStatementExecutor = executor
            }
            .ExecuteAsync(context, script))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(exception!.StatementIndex).IsEqualTo(0);
        await Assert.That(exception.Stage).IsEqualTo(LoadScriptStage.FinalTableWrite);
        await Assert.That(exception.Span).IsEqualTo(statement.LoadSpan);
        await Assert.That(exception.InnerException).IsTypeOf<LoadScriptExecutionException>();
        await Assert.That(exception.InnerException!.InnerException).IsTypeOf<InvalidOperationException>();
    }

    [Test]
    public async Task Execute_load_wraps_duplicate_select_alias_as_query_resolution_script_exception()
    {
        var executor = new TestLoadStatementExecutor
        {
            ProviderResolver = new FakeProviderResolver()
        };
        var context = CreateContext();
        var script = Loader.Lang.Script.Parse(
            """
            orders: LOAD
                name AS city,
                id AS city
            FROM Csv(path='orders.csv');
            """).Value!;
        var statement = (LoadStatement)script.Statements[0];
        var duplicateSpan = statement.Fields![1].Span;

        var exception = await Assert.That(async () => await new ScriptExecutor
            {
                LoadStatementExecutor = executor
            }
            .ExecuteAsync(context, script))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(exception!.StatementIndex).IsEqualTo(0);
        await Assert.That(exception.Stage).IsEqualTo(LoadScriptStage.QueryResolution);
        await Assert.That(exception.Span).IsEqualTo(duplicateSpan);
        await Assert.That(exception.InnerException).IsTypeOf<QueryResolutionException>();
        await Assert.That(exception.InnerException!.Message).Contains("LOAD select alias 'city' is duplicated.");
    }

    [Test]
    [DisplayName("ScriptExecutor оборачивает provider options как ProviderResolution ошибку")]
    public async Task Execute_load_wraps_provider_option_errors_as_provider_resolution_script_exception()
    {
        var context = CreateContext();
        var script = Loader.Lang.Script.Parse(
            """
            orders: LOAD *
            FROM UnknownProvider(connection='Host=localhost;Database=db');
            """).Value!;

        var exception = await Assert.That(async () => await new ScriptExecutor()
            .ExecuteAsync(context, script))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(exception!.StatementIndex).IsEqualTo(0);
        await Assert.That(exception.Stage).IsEqualTo(LoadScriptStage.ProviderResolution);
        await Assert.That(exception.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.InnerException).IsTypeOf<ProviderResolutionException>();
        await Assert.That(exception.Errors.Select(static error => error.Message).ToArray())
            .Contains("Provider 'unknownprovider' не поддерживается.");
    }

    [Test]
    [DisplayName("ScriptExecutor оборачивает Inline transformations как ProviderResolution ошибку")]
    public async Task Execute_load_wraps_inline_transformations_as_provider_resolution_script_exception()
    {
        var context = CreateContext();
        var script = Loader.Lang.Script.Parse(
            """
            bad_inline_where:
            LOAD *
            FROM Inline(id, name; 1, 'Mike'; 2, 'Ann')
            WHERE id > 1;
            """).Value!;
        var statement = (LoadStatement)script.Statements[0];

        var exception = await Assert.That(async () => await new ScriptExecutor()
            .ExecuteAsync(context, script))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(exception!.StatementIndex).IsEqualTo(0);
        await Assert.That(exception.Stage).IsEqualTo(LoadScriptStage.ProviderResolution);
        await Assert.That(exception.Span).IsEqualTo(statement.WhereSpan);
        await Assert.That(exception.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.InnerException).IsTypeOf<ProviderResolutionException>();
        await Assert.That(exception.Errors[0].Message).Contains("WHERE");
        await Assert.That(exception.Errors[0].Message).Contains("отдельный LOAD");
    }

    [Test]
    [DisplayName("ScriptExecutor оборачивает ошибку подготовки JSON provider с statement и span")]
    public async Task Execute_load_wraps_json_provider_prepare_error_as_script_exception()
    {
        var context = CreateContext(new ThrowingFileSource());
        var script = Loader.Lang.Script.Parse(
            """
            orders: LOAD *
            FROM Json(path='missing.json');
            """).Value!;
        var statement = (LoadStatement)script.Statements[0];

        var exception = await Assert.That(async () => await new ScriptExecutor()
            .ExecuteAsync(context, script))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(exception!.StatementIndex).IsEqualTo(0);
        await Assert.That(exception.Stage).IsEqualTo(LoadScriptStage.ProviderResolution);
        await Assert.That(exception.Span).IsEqualTo(statement.SourceCall.Span);
        await Assert.That(exception.InnerException).IsTypeOf<ProviderResolutionException>();
        await Assert.That(exception.InnerException!.InnerException).IsTypeOf<JsonFileOpenProviderException>();
    }

    [Test]
    [DisplayName("ScriptExecutor оборачивает LIMIT 0 как QueryResolution ошибку")]
    public async Task Execute_load_wraps_limit_zero_as_query_resolution_script_exception()
    {
        var executor = new TestLoadStatementExecutor
        {
            ProviderResolver = new FakeProviderResolver()
        };
        var context = CreateContext();
        var script = Loader.Lang.Script.Parse(
            """
            orders: LOAD
                name AS city
            FROM Csv(path='orders.csv')
            LIMIT 0;
            """).Value!;
        var statement = (LoadStatement)script.Statements[0];

        var exception = await Assert.That(async () => await new ScriptExecutor
            {
                LoadStatementExecutor = executor
            }
            .ExecuteAsync(context, script))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(exception!.StatementIndex).IsEqualTo(0);
        await Assert.That(exception.Stage).IsEqualTo(LoadScriptStage.QueryResolution);
        await Assert.That(exception.Span).IsEqualTo(statement.LimitPart!.Span);
        await Assert.That(exception.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.InnerException).IsTypeOf<QueryResolutionException>();
        await Assert.That(exception.InnerException!.Message).Contains("LIMIT 0");
    }

    [Test]
    [DisplayName("ScriptExecutor оборачивает WHERE не boolean как QueryResolution ошибку")]
    public async Task Execute_load_wraps_non_boolean_where_as_query_resolution_script_exception()
    {
        var executor = new TestLoadStatementExecutor
        {
            ProviderResolver = new FakeProviderResolver()
        };
        var context = CreateContext();
        var script = Loader.Lang.Script.Parse(
            """
            orders: LOAD
                name AS city
            FROM Csv(path='orders.csv')
            WHERE id;
            """).Value!;
        var statement = (LoadStatement)script.Statements[0];

        var exception = await Assert.That(async () => await new ScriptExecutor
            {
                LoadStatementExecutor = executor
            }
            .ExecuteAsync(context, script))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(exception!.StatementIndex).IsEqualTo(0);
        await Assert.That(exception.Stage).IsEqualTo(LoadScriptStage.QueryResolution);
        await Assert.That(exception.Span).IsEqualTo(statement.Where!.Span);
        await Assert.That(exception.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.InnerException).IsTypeOf<QueryResolutionException>();
        await Assert.That(exception.InnerException!.Message).Contains("WHERE expression");
    }

    [Test]
    [DisplayName("ScriptExecutor оборачивает LOAD * GROUP BY как QueryResolution ошибку")]
    public async Task Execute_load_wraps_select_all_group_by_as_query_resolution_script_exception()
    {
        var executor = new TestLoadStatementExecutor
        {
            ProviderResolver = new FakeProviderResolver()
        };
        var context = CreateContext();
        var script = Loader.Lang.Script.Parse(
            """
            orders: LOAD *
            FROM Csv(path='orders.csv')
            GROUP BY name;
            """).Value!;
        var statement = (LoadStatement)script.Statements[0];

        var exception = await Assert.That(async () => await new ScriptExecutor
            {
                LoadStatementExecutor = executor
            }
            .ExecuteAsync(context, script))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(exception!.StatementIndex).IsEqualTo(0);
        await Assert.That(exception.Stage).IsEqualTo(LoadScriptStage.QueryResolution);
        await Assert.That(exception.Span).IsEqualTo(statement.GroupBy![0].Span);
        await Assert.That(exception.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.InnerException).IsTypeOf<QueryResolutionException>();
        await Assert.That(exception.InnerException!.Message).Contains("SELECT *");
    }

    private static ScriptContext CreateContext(
        IFileSource? fileSource = null,
        IProgressLogger? logger = null)
    {
        return new ScriptContext
        {
            FileStorage = fileSource ?? new StubFileSource(),
            TargetConnectionString = "Host=localhost",
            Logger = logger ?? NullProgressLogger.Instance,
            Options = new ScriptContextOptions
            {
                TempTablePrefix = "tmp_",
                FinalTablePrefix = "final_"
            }
        };
    }

    private static LangSpan Span()
    {
        return new LangSpan(1, 1, 1, 1);
    }

    private static LoadSourceCall SourceCall(string provider, string path)
    {
        return new LoadSourceCall
        {
            Name = provider,
            NameSpan = Span(),
            Options =
            [
                new Loader.Lang.Statements.LoadOption
                {
                    Name = "path",
                    Span = Span(),
                    Value = new StringLiteral(path)
                }
            ],
            Span = Span()
        };
    }

    private sealed class FakeProviderResolver : ILoadProviderResolver
    {
        public int ResolveCalls { get; private set; }

        public string SourceKind { get; init; } = "fake";

        public ValueTask<LoadProviderSource> ResolveAsync(
            LoadStatement statement,
            ScriptContext context,
            CancellationToken cancellationToken = default)
        {
            ResolveCalls++;
            return ValueTask.FromResult(new LoadProviderSource
            {
                Kind = SourceKind,
                RequiresBuffer = false,
                OpenReaderAsync = _ => ValueTask.FromResult<DbDataReader>(CreateReader())
            });
        }

        private static DbDataReader CreateReader()
        {
            var table = new DataTable();
            table.Columns.Add("id", typeof(int));
            table.Columns.Add("name", typeof(string));
            table.Rows.Add(1, "Moscow");
            return table.CreateDataReader();
        }
    }

    private sealed class TestLoadStatementExecutor : LoadStatementExecutor
    {
        public int WriteCalls { get; private set; }

        public ClickHouseTableName? TableName { get; private set; }

        public int MaterializeCalls { get; private set; }

        public ClickHouseTableName? FinalTableName { get; private set; }

        public int DropCalls { get; private set; }

        public ClickHouseTableName? DropTableName { get; private set; }

        public int DropFinalCalls { get; private set; }

        public ClickHouseTableName? DropFinalTableName { get; private set; }

        public string? QuerySql { get; private set; }

        public bool ThrowOnMaterialize { get; init; }

        public long FinalRowCount { get; init; }

        public List<object[]> Rows { get; } = [];

        protected override async ValueTask<long> WriteTempTableAsync(
            ScriptContext context,
            DomainDataReader reader,
            ClickHouseTableName tableName,
            CancellationToken cancellationToken)
        {
            WriteCalls++;
            TableName = tableName;

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var values = new object[reader.FieldCount];
                reader.GetValues(values);
                Rows.Add(values);
            }

            return Rows.Count;
        }

        protected override ValueTask<long> MaterializeFinalTableAsync(
            ScriptContext context,
            string querySql,
            ClickHouseTableName finalTable,
            CancellationToken cancellationToken)
        {
            MaterializeCalls++;
            QuerySql = querySql;
            FinalTableName = finalTable;
            if (ThrowOnMaterialize)
            {
                throw new InvalidOperationException("materialize failed");
            }

            return ValueTask.FromResult(FinalRowCount);
        }

        protected override ValueTask DropTempTableAsync(
            ScriptContext context,
            ClickHouseTableName tempTable,
            CancellationToken cancellationToken)
        {
            DropCalls++;
            DropTableName = tempTable;
            return ValueTask.CompletedTask;
        }

        protected override ValueTask DropFinalTableAsync(
            ScriptContext context,
            ClickHouseTableName finalTable,
            CancellationToken cancellationToken)
        {
            DropFinalCalls++;
            DropFinalTableName = finalTable;
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

    private sealed class ThrowingFileSource : IFileSource
    {
        public Stream OpenRead(string fileName)
        {
            throw new FileNotFoundException("missing", fileName);
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
}

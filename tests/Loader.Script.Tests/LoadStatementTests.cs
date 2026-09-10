using System.Data;
using System.Data.Common;
using Loader.Core.Decorators;
using Loader.Core.Exceptions;
using Loader.Core.Models;
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
        await Assert.That(executor.QuerySql).Contains(".`column2` AS `column1`");
        await Assert.That(executor.QuerySql).Contains("WHERE (source_");
        await Assert.That(executor.QuerySql).Contains(".`column1` > 0)");
        await Assert.That(executor.QuerySql).Contains("ORDER BY source_");
        await Assert.That(executor.QuerySql).Contains(".`column2` ASC");
        await Assert.That(executor.QuerySql).Contains("LIMIT 10");
        await Assert.That(executor.QuerySql).Contains("OFFSET 1");
        await Assert.That(loadedTable.Name).IsSameReferenceAs(executor.FinalTableName);
        await Assert.That(loadedTable.Alias).IsEqualTo("orders");
        await Assert.That(loadedTable.Kind).IsEqualTo(LoadedTableKind.Normal);
        await Assert.That(loadedTable.Fields).Count().IsEqualTo(1);
        await Assert.That(loadedTable.Fields[0].Name).IsEqualTo("city");
        await Assert.That(context.LoadedTables).Count().IsEqualTo(1);
        await Assert.That(context.LoadedTables[0]).IsSameReferenceAs(loadedTable);
    }

    [Test]
    [DisplayName("Execute FROM table использует prepared SQL source без temp table")]
    public async Task Execute_load_from_table_uses_prepared_sql_source_without_temp_table()
    {
        var executor = new TestLoadStatementExecutor();
        var context = CreateContext();
        context.AddLoadedTable(LoadedTable("source", LoadedTableKind.Normal));
        var script = Loader.Lang.Script.Parse(
            """
            result:
            LOAD
                value AS mapped_value
            FROM source;
            """).Value!;

        var loadedTable = await executor.ExecuteAsync(context, (LoadStatement)script.Statements[0]);

        await Assert.That(executor.WriteCalls).IsEqualTo(0);
        await Assert.That(executor.DropCalls).IsEqualTo(0);
        await Assert.That(executor.MaterializeCalls).IsEqualTo(1);
        await Assert.That(executor.QuerySql).Contains("FROM `physical_source` AS source_");
        await Assert.That(executor.QuerySql).Contains(".`column2` AS `column1`");
        await Assert.That(loadedTable.Alias).IsEqualTo("result");
        await Assert.That(loadedTable.Fields[0].Name).IsEqualTo("mapped_value");
    }

    [Test]
    [DisplayName("Execute TEMP LOAD регистрирует временную таблицу")]
    public async Task Execute_temp_load_registers_temp_loaded_table()
    {
        var executor = new TestLoadStatementExecutor
        {
            ProviderResolver = new FakeProviderResolver()
        };
        var context = CreateContext();
        var script = Loader.Lang.Script.Parse(
            """
            orders:
            TEMP LOAD
                name AS city
            FROM Csv(path='orders.csv');
            """).Value!;
        var statement = (LoadStatement)script.Statements[0];

        var loadedTable = await executor.ExecuteAsync(context, statement);

        await Assert.That(statement.IsTemporary).IsTrue();
        await Assert.That(loadedTable.Kind).IsEqualTo(LoadedTableKind.Temp);
        await Assert.That(context.LoadedTables).Count().IsEqualTo(1);
        await Assert.That(context.LoadedTables[0].Kind).IsEqualTo(LoadedTableKind.Temp);
    }

    [Test]
    [DisplayName("ScriptExecutor запрещает повторный LOAD с уже занятым именем таблицы")]
    public async Task Execute_script_rejects_repeated_load_table_name()
    {
        var executor = new TestLoadStatementExecutor
        {
            ProviderResolver = new FakeProviderResolver()
        };
        var context = CreateContext();
        var script = Loader.Lang.Script.Parse(
            """
            orders:
            LOAD
                name AS city
            FROM Inline(x; 1);

            orders:
            LOAD
                id
            FROM Inline(x; 1);
            """).Value!;
        var repeatedStatement = (LoadStatement)script.Statements[1];

        var exception = await Assert.That(async () => await new ScriptExecutor
        {
            LoadStatementExecutor = executor
        }.ExecuteAsync(context, script))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(exception!.StatementIndex).IsEqualTo(1);
        await Assert.That(exception.Stage).IsEqualTo(LoadScriptStage.QueryResolution);
        await Assert.That(exception.Span).IsEqualTo(repeatedStatement.TableNameSpan);
        await Assert.That(exception.InnerException).IsTypeOf<QueryResolutionException>();
        await Assert.That(exception.InnerException!.Message).Contains("Имя LOAD таблицы 'orders' уже занято.");
        await Assert.That(executor.MaterializeCalls).IsEqualTo(1);
    }

    [Test]
    [DisplayName("ScriptExecutor требует имя таблицы у LOAD на semantic validation")]
    public async Task Execute_script_rejects_load_without_table_name()
    {
        var executor = new TestLoadStatementExecutor
        {
            ProviderResolver = new FakeProviderResolver()
        };
        var context = CreateContext();
        var script = Loader.Lang.Script.Parse("LOAD * FROM Inline(x; 1);").Value!;
        var statement = (LoadStatement)script.Statements[0];

        var exception = await Assert.That(async () => await new ScriptExecutor
        {
            LoadStatementExecutor = executor
        }.ExecuteAsync(context, script))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(exception!.StatementIndex).IsEqualTo(0);
        await Assert.That(exception.Stage).IsEqualTo(LoadScriptStage.QueryResolution);
        await Assert.That(exception.Span).IsEqualTo(statement.LoadSpan);
        await Assert.That(exception.InnerException).IsTypeOf<QueryResolutionException>();
        await Assert.That(exception.InnerException!.Message).Contains("У LOAD должно быть имя таблицы.");
        await Assert.That(exception.InnerException!.Message).Contains("table_name: LOAD");
        await Assert.That(((FakeProviderResolver)executor.ProviderResolver).ResolveCalls).IsEqualTo(0);
        await Assert.That(executor.MaterializeCalls).IsEqualTo(0);
    }

    [Test]
    [DisplayName("Execute MAPPED LOAD с явными полями требует ровно key и value")]
    public async Task Execute_mapped_load_with_explicit_fields_rejects_not_two_fields()
    {
        var executor = new TestLoadStatementExecutor
        {
            ProviderResolver = new FakeProviderResolver()
        };
        var context = CreateContext();
        var script = Loader.Lang.Script.Parse(
            """
            map:
            MAPPED LOAD
                id AS key,
                name AS value,
                name AS extra
            FROM Csv(path='orders.csv');
            """).Value!;
        var statement = (LoadStatement)script.Statements[0];

        var exception = await Assert.That(async () => await new ScriptExecutor
        {
            LoadStatementExecutor = executor
        }.ExecuteAsync(context, script))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(exception!.Stage).IsEqualTo(LoadScriptStage.QueryResolution);
        await Assert.That(exception.Span).IsEqualTo(statement.KindSpan);
        await Assert.That(exception.InnerException).IsTypeOf<QueryResolutionException>();
        await Assert.That(exception.InnerException!.Message).Contains("MAPPED LOAD");
        await Assert.That(((FakeProviderResolver)executor.ProviderResolver).ResolveCalls).IsEqualTo(0);
    }

    [Test]
    [DisplayName("Execute MAPPED LOAD * требует source reader ровно с двумя полями")]
    public async Task Execute_mapped_load_star_rejects_source_reader_not_two_fields()
    {
        var executor = new TestLoadStatementExecutor
        {
            ProviderResolver = new FakeProviderResolver
            {
                ColumnNames = ["key", "value", "extra"],
                RowValues = [1, "Moscow", "extra"]
            }
        };
        var context = CreateContext();
        var script = Loader.Lang.Script.Parse(
            """
            map:
            MAPPED LOAD *
            FROM Csv(path='orders.csv');
            """).Value!;
        var statement = (LoadStatement)script.Statements[0];

        var exception = await Assert.That(async () => await new ScriptExecutor
        {
            LoadStatementExecutor = executor
        }.ExecuteAsync(context, script))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(exception!.Stage).IsEqualTo(LoadScriptStage.QueryResolution);
        await Assert.That(exception.Span).IsEqualTo(statement.KindSpan);
        await Assert.That(exception.InnerException).IsTypeOf<QueryResolutionException>();
        await Assert.That(exception.InnerException!.Message).Contains("ожидалось 2");
        await Assert.That(executor.WriteCalls).IsEqualTo(0);
        await Assert.That(executor.MaterializeCalls).IsEqualTo(0);
    }

    [Test]
    [DisplayName("Execute LOAD ругается если provider вернул ноль полей")]
    public async Task Execute_load_rejects_source_reader_with_zero_fields()
    {
        var executor = new TestLoadStatementExecutor
        {
            ProviderResolver = new FakeProviderResolver
            {
                SourceKind = "connect",
                ColumnNames = [],
                RowValues = []
            }
        };
        var context = CreateContext();
        var script = Loader.Lang.Script.Parse(
            """
            orders:
            LOAD *
            FROM Connect(name='pg') SQL 'select nothing';
            """).Value!;
        var statement = (LoadStatement)script.Statements[0];

        var exception = await Assert.That(async () => await new ScriptExecutor
        {
            LoadStatementExecutor = executor
        }.ExecuteAsync(context, script))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(exception!.Stage).IsEqualTo(LoadScriptStage.QueryResolution);
        await Assert.That(exception.Span).IsEqualTo(statement.SourceCall.Span);
        await Assert.That(exception.InnerException).IsTypeOf<QueryResolutionException>();
        await Assert.That(exception.InnerException!.Message).Contains("Источник вернул ноль полей.");
        await Assert.That(executor.WriteCalls).IsEqualTo(0);
        await Assert.That(executor.MaterializeCalls).IsEqualTo(0);
    }

    [Test]
    [DisplayName("Execute ApplyMap ругается если mapping-таблица не найдена")]
    public async Task Execute_apply_map_rejects_missing_mapped_table()
    {
        var executor = new TestLoadStatementExecutor
        {
            ProviderResolver = new FakeProviderResolver()
        };
        var context = CreateContext();
        var script = Loader.Lang.Script.Parse(
            """
            orders:
            LOAD
                name.ApplyMap('missing_map') AS mapped_name
            FROM Csv(path='orders.csv');
            """).Value!;

        var exception = await Assert.That(async () => await new ScriptExecutor
        {
            LoadStatementExecutor = executor
        }.ExecuteAsync(context, script))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(exception!.Stage).IsEqualTo(LoadScriptStage.QueryResolution);
        await Assert.That(exception.InnerException).IsTypeOf<QueryResolutionException>();
        await Assert.That(exception.InnerException!.Message).Contains("missing_map");
        await Assert.That(exception.InnerException!.Message).Contains("не найдена");
        await Assert.That(executor.MaterializeCalls).IsEqualTo(0);
    }

    [Test]
    [DisplayName("Execute ApplyMap ругается если таблица существует, но не является MAPPED LOAD")]
    public async Task Execute_apply_map_rejects_non_mapped_loaded_table()
    {
        var executor = new TestLoadStatementExecutor
        {
            ProviderResolver = new FakeProviderResolver()
        };
        var context = CreateContext();
        context.AddLoadedTable(LoadedTable("not_map", LoadedTableKind.Normal));
        var script = Loader.Lang.Script.Parse(
            """
            orders:
            LOAD
                name.ApplyMap('not_map') AS mapped_name
            FROM Csv(path='orders.csv');
            """).Value!;

        var exception = await Assert.That(async () => await new ScriptExecutor
        {
            LoadStatementExecutor = executor
        }.ExecuteAsync(context, script))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(exception!.Stage).IsEqualTo(LoadScriptStage.QueryResolution);
        await Assert.That(exception.InnerException).IsTypeOf<QueryResolutionException>();
        await Assert.That(exception.InnerException!.Message).Contains("not_map");
        await Assert.That(exception.InnerException!.Message).Contains("не является MAPPED LOAD");
        await Assert.That(executor.MaterializeCalls).IsEqualTo(0);
    }

    [Test]
    [DisplayName("Execute ApplyMap строит joinGetOrNull и берет тип значения из MAPPED LOAD")]
    public async Task Execute_apply_map_uses_mapped_table_metadata()
    {
        var executor = new TestLoadStatementExecutor
        {
            ProviderResolver = new FakeProviderResolver()
        };
        var context = CreateContext();
        context.AddLoadedTable(LoadedTable("map", LoadedTableKind.Mapped));
        var script = Loader.Lang.Script.Parse(
            """
            orders:
            LOAD
                name.ApplyMap('map') AS mapped_name
            FROM Csv(path='orders.csv');
            """).Value!;

        var table = await executor.ExecuteAsync(context, (LoadStatement)script.Statements[0]);

        await Assert.That(executor.QuerySql).Contains("joinGetOrNull('physical_map', 'column2', source_");
        await Assert.That(executor.QuerySql).Contains(".`column2`)");
        await Assert.That(table.Fields).Count().IsEqualTo(1);
        await Assert.That(table.Fields[0].Name).IsEqualTo("mapped_name");
        await Assert.That(table.Fields[0].DataType).IsEqualTo(DataType.Text);
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

        var loadedTable = await executor.ExecuteAsync(context, statement);

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
        await Assert.That(loadedTable.RowCount).IsEqualTo(1);
    }

    [Test]
    [DisplayName("Execute LOAD обновляет progress записи final table одним messageId")]
    public async Task Execute_load_updates_final_table_write_progress_with_same_message_id()
    {
        var logger = new RecordingProgressLogger();
        var executor = new TestLoadStatementExecutor
        {
            FinalRowCount = 3,
            FinalMaterializeDelay = TimeSpan.FromMilliseconds(50)
        };
        var context = CreateContext(
            logger: logger,
            options: new ScriptContextOptions
            {
                TempTablePrefix = "tmp_",
                FinalTablePrefix = "final_",
                FinalTableWriteHeartbeatInterval = TimeSpan.FromMilliseconds(5)
            });
        context.AddLoadedTable(LoadedTable("source", LoadedTableKind.Normal));
        var script = Loader.Lang.Script.Parse(
            """
            result:
            LOAD *
            FROM source;
            """).Value!;

        await executor.ExecuteAsync(context, (LoadStatement)script.Statements[0]);

        var finalEvents = logger.Events
            .Where(static item => item.Kind is "TransformationWriteStarted" or "TransformationRowsLoaded")
            .ToArray();
        await Assert.That(finalEvents.Length).IsGreaterThanOrEqualTo(3);
        await Assert.That(finalEvents.Select(static item => item.MessageId).Distinct().Count()).IsEqualTo(1);
        await Assert.That(finalEvents[0].MessageId).IsNotNull();
        await Assert.That(finalEvents[0].Message).IsEqualTo("Загружаем таблицу. Прошло 0 секунд.");
        await Assert.That(finalEvents[^1].Kind).IsEqualTo("TransformationRowsLoaded");
        await Assert.That(finalEvents[^1].Message).Contains("Загружено 3 записей за ");
        await Assert.That(logger.Events.Where(static item => item.Kind == "LoadTableStarted").All(static item => item.MessageId is null)).IsTrue();
    }

    [Test]
    [DisplayName("Execute LOAD обновляет progress загрузки source reader одним messageId")]
    public async Task Execute_load_updates_source_reader_progress_with_same_message_id()
    {
        var logger = new RecordingProgressLogger();
        var executor = new TestLoadStatementExecutor
        {
            ProviderResolver = new FakeProviderResolver
            {
                SourceKind = "csv",
                Rows =
                [
                    [1, "Moscow"],
                    [2, "Paris"],
                    [3, "Berlin"]
                ]
            },
            FinalRowCount = 3
        };
        var context = CreateContext(
            logger: logger,
            options: new ScriptContextOptions
            {
                TempTablePrefix = "tmp_",
                FinalTablePrefix = "final_",
                SourceRowsProgressInterval = TimeSpan.Zero
            });
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

        await executor.ExecuteAsync(context, statement);

        var sourceEvents = logger.Events
            .Where(static item => item.Kind == "SourceRowsLoaded")
            .ToArray();
        await Assert.That(sourceEvents).Count().IsEqualTo(4);
        await Assert.That(sourceEvents[0].Message).StartsWith("Выгружаем 1 записей. Прошло ");
        await Assert.That(sourceEvents[1].Message).StartsWith("Выгружаем 2 записей. Прошло ");
        await Assert.That(sourceEvents[2].Message).StartsWith("Выгружаем 3 записей. Прошло ");
        await Assert.That(sourceEvents[3].Message).StartsWith("Выгружено 3 записей. Заняло ");
        await Assert.That(sourceEvents.Select(static item => item.MessageId).Distinct().Count()).IsEqualTo(1);
        await Assert.That(sourceEvents[0].MessageId).IsNotNull();
        await Assert.That(logger.Events
            .Where(static item => item.Kind != "SourceRowsLoaded" && !item.Kind.StartsWith("Transformation", StringComparison.Ordinal))
            .All(static item => item.MessageId is null)).IsTrue();
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
        await Assert.That(executor.QuerySql).Contains(".`column1` AS `column1`");
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
    [DisplayName("ScriptExecutor очищает TEMP LOAD и возвращает только обычные таблицы")]
    public async Task Execute_script_cleans_temp_loaded_tables_and_returns_normal_tables()
    {
        var executor = new TestLoadStatementExecutor
        {
            ProviderResolver = new FakeProviderResolver()
        };
        var cleanup = new RecordingTemporaryCleanupExecutor();
        var context = CreateContext();
        var script = Loader.Lang.Script.Parse(
            """
            raw:
            TEMP LOAD
                name AS city
            FROM Csv(path='orders.csv');

            final:
            LOAD
                name AS city
            FROM Csv(path='orders.csv');
            """).Value!;

        var result = await new ScriptExecutor
        {
            LoadStatementExecutor = executor,
            TemporaryTableCleanupExecutor = cleanup
        }.ExecuteAsync(context, script);

        await Assert.That(cleanup.ExecuteCalls).IsEqualTo(1);
        await Assert.That(cleanup.CleanedAliases).IsEquivalentTo(["raw"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(context.LoadedTables.Select(static table => table.Alias!).ToArray())
            .IsEquivalentTo(["final"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(result.Select(static table => table.Alias!).ToArray())
            .IsEquivalentTo(["final"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(result.All(static table => table.Kind == LoadedTableKind.Normal)).IsTrue();
    }

    [Test]
    [DisplayName("ScriptExecutor возвращает пустой result если script создал только TEMP LOAD")]
    public async Task Execute_script_returns_empty_result_when_only_temp_loads_were_created()
    {
        var executor = new TestLoadStatementExecutor
        {
            ProviderResolver = new FakeProviderResolver()
        };
        var cleanup = new RecordingTemporaryCleanupExecutor();
        var context = CreateContext();
        var script = Loader.Lang.Script.Parse(
            """
            raw:
            TEMP LOAD
                name AS city
            FROM Csv(path='orders.csv');
            """).Value!;

        var result = await new ScriptExecutor
        {
            LoadStatementExecutor = executor,
            TemporaryTableCleanupExecutor = cleanup
        }.ExecuteAsync(context, script);

        await Assert.That(cleanup.ExecuteCalls).IsEqualTo(1);
        await Assert.That(cleanup.CleanedAliases).IsEquivalentTo(["raw"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(context.LoadedTables).IsEmpty();
        await Assert.That(result).IsEmpty();
    }

    [Test]
    [DisplayName("ScriptExecutor чистит TEMP LOAD best-effort если следующий statement падает")]
    public async Task Execute_script_cleans_temp_loaded_tables_best_effort_on_failure()
    {
        var executor = new TestLoadStatementExecutor
        {
            ProviderResolver = new FakeProviderResolver(),
            ThrowOnSecondMaterialize = true
        };
        var cleanup = new RecordingTemporaryCleanupExecutor();
        var context = CreateContext();
        var script = Loader.Lang.Script.Parse(
            """
            raw:
            TEMP LOAD
                name AS city
            FROM Csv(path='orders.csv');

            broken:
            LOAD
                name AS city
            FROM Csv(path='orders.csv');
            """).Value!;

        await Assert.That(async () => await new ScriptExecutor
        {
            LoadStatementExecutor = executor,
            TemporaryTableCleanupExecutor = cleanup
        }.ExecuteAsync(context, script))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(cleanup.BestEffortCalls).IsEqualTo(1);
        await Assert.That(cleanup.CleanedAliases).IsEquivalentTo(["raw"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
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
    [DisplayName("LOAD field без AS получает alias из единственного поля выражения")]
    public async Task Execute_load_infers_select_alias_from_single_name_expression()
    {
        var executor = new TestLoadStatementExecutor
        {
            ProviderResolver = new FakeProviderResolver()
        };
        var context = CreateContext();
        var script = Loader.Lang.Script.Parse(
            """
            orders:
            LOAD
                id.Int(),
                name
            FROM Csv(path='orders.csv');
            """).Value!;

        var loadedTable = await executor.ExecuteAsync(context, (LoadStatement)script.Statements[0]);

        await Assert.That(loadedTable.Fields.Select(static field => field.Name).ToArray())
            .IsEquivalentTo(["id", "name"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("LOAD field без AS получает alias если выражение повторяет одно поле")]
    public async Task Execute_load_infers_select_alias_from_repeated_same_name_expression()
    {
        var executor = new TestLoadStatementExecutor
        {
            ProviderResolver = new FakeProviderResolver()
        };
        var context = CreateContext();
        var script = Loader.Lang.Script.Parse(
            """
            orders:
            LOAD id + id
            FROM Csv(path='orders.csv');
            """).Value!;

        var loadedTable = await executor.ExecuteAsync(context, (LoadStatement)script.Statements[0]);

        await Assert.That(loadedTable.Fields.Select(static field => field.Name).ToArray())
            .IsEquivalentTo(["id"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("LOAD field без AS требует alias если выражение использует несколько полей")]
    public async Task Execute_load_requires_select_alias_for_expression_with_multiple_names()
    {
        var executor = new TestLoadStatementExecutor
        {
            ProviderResolver = new FakeProviderResolver()
        };
        var context = CreateContext();
        var script = Loader.Lang.Script.Parse(
            """
            orders:
            LOAD id + name
            FROM Csv(path='orders.csv');
            """).Value!;
        var statement = (LoadStatement)script.Statements[0];
        var expressionSpan = statement.Fields![0].Expression.Span;

        var exception = await Assert.That(async () => await new ScriptExecutor
            {
                LoadStatementExecutor = executor
            }
            .ExecuteAsync(context, script))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(exception!.StatementIndex).IsEqualTo(0);
        await Assert.That(exception.Stage).IsEqualTo(LoadScriptStage.QueryResolution);
        await Assert.That(exception.Span).IsEqualTo(expressionSpan);
        await Assert.That(exception.InnerException).IsTypeOf<QueryResolutionException>();
        await Assert.That(exception.InnerException!.Message).Contains("Не указано имя поля. Напишите AS [Название].");
        await Assert.That(((FakeProviderResolver)executor.ProviderResolver).ResolveCalls).IsEqualTo(0);
    }

    [Test]
    [DisplayName("LOAD field без AS требует alias если выражение не использует поля")]
    public async Task Execute_load_requires_select_alias_for_expression_without_names()
    {
        var executor = new TestLoadStatementExecutor
        {
            ProviderResolver = new FakeProviderResolver()
        };
        var context = CreateContext();
        var script = Loader.Lang.Script.Parse(
            """
            orders:
            LOAD 1
            FROM Csv(path='orders.csv');
            """).Value!;
        var statement = (LoadStatement)script.Statements[0];
        var expressionSpan = statement.Fields![0].Expression.Span;

        var exception = await Assert.That(async () => await new ScriptExecutor
            {
                LoadStatementExecutor = executor
            }
            .ExecuteAsync(context, script))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(exception!.StatementIndex).IsEqualTo(0);
        await Assert.That(exception.Stage).IsEqualTo(LoadScriptStage.QueryResolution);
        await Assert.That(exception.Span).IsEqualTo(expressionSpan);
        await Assert.That(exception.InnerException).IsTypeOf<QueryResolutionException>();
        await Assert.That(exception.InnerException!.Message).Contains("Не указано имя поля. Напишите AS [Название].");
        await Assert.That(((FakeProviderResolver)executor.ProviderResolver).ResolveCalls).IsEqualTo(0);
    }

    [Test]
    [DisplayName("LOAD inferred alias участвует в проверке дублей")]
    public async Task Execute_load_rejects_duplicate_inferred_select_alias()
    {
        var executor = new TestLoadStatementExecutor
        {
            ProviderResolver = new FakeProviderResolver()
        };
        var context = CreateContext();
        var script = Loader.Lang.Script.Parse(
            """
            orders:
            LOAD
                name AS id,
                id.Int()
            FROM Csv(path='orders.csv');
            """).Value!;
        var statement = (LoadStatement)script.Statements[0];
        var expressionSpan = statement.Fields![1].Expression.Span;

        var exception = await Assert.That(async () => await new ScriptExecutor
            {
                LoadStatementExecutor = executor
            }
            .ExecuteAsync(context, script))
            .ThrowsExactly<LoadScriptException>();

        await Assert.That(exception!.StatementIndex).IsEqualTo(0);
        await Assert.That(exception.Stage).IsEqualTo(LoadScriptStage.QueryResolution);
        await Assert.That(exception.Span).IsEqualTo(expressionSpan);
        await Assert.That(exception.InnerException).IsTypeOf<QueryResolutionException>();
        await Assert.That(exception.InnerException!.Message).Contains("LOAD select alias 'id' is duplicated.");
        await Assert.That(((FakeProviderResolver)executor.ProviderResolver).ResolveCalls).IsEqualTo(0);
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
    [DisplayName("ScriptExecutor оборачивает агрегат в WHERE как QueryResolution ошибку")]
    public async Task Execute_load_wraps_aggregate_where_as_query_resolution_script_exception()
    {
        var executor = new TestLoadStatementExecutor
        {
            ProviderResolver = new FakeProviderResolver
            {
                ColumnNames = ["id", "amount"],
                RowValues = [1, 10m]
            }
        };
        var context = CreateContext();
        var script = Loader.Lang.Script.Parse(
            """
            orders: LOAD
                id
            FROM Csv(path='orders.csv')
            WHERE SUM(amount) > 0
            GROUP BY id;
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
        await Assert.That(exception.InnerException!.Message).Contains("WHERE не может содержать агрегатные выражения");
        await Assert.That(executor.MaterializeCalls).IsEqualTo(0);
    }

    [Test]
    [DisplayName("ScriptExecutor оборачивает некорректный агрегат в ORDER BY как QueryResolution ошибку")]
    public async Task Execute_load_wraps_invalid_aggregate_order_by_as_query_resolution_script_exception()
    {
        var executor = new TestLoadStatementExecutor
        {
            ProviderResolver = new FakeProviderResolver
            {
                ColumnNames = ["id", "amount"],
                RowValues = [1, 10m]
            }
        };
        var context = CreateContext();
        var script = Loader.Lang.Script.Parse(
            """
            orders: LOAD
                id
            FROM Csv(path='orders.csv')
            ORDER BY SUM(amount) DESC;
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
        await Assert.That(exception.Span).IsEqualTo(statement.Fields![0].Span);
        await Assert.That(exception.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.InnerException).IsTypeOf<QueryResolutionException>();
        await Assert.That(exception.InnerException!.Message).Contains("SELECT expression 'id' должен быть агрегирован или вынесен в GROUP BY");
        await Assert.That(executor.MaterializeCalls).IsEqualTo(0);
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
        IProgressLogger? logger = null,
        ScriptContextOptions? options = null)
    {
        return new ScriptContext
        {
            FileStorage = fileSource ?? new StubFileSource(),
            TargetConnectionString = "Host=localhost",
            Logger = logger ?? NullProgressLogger.Instance,
            Options = options ?? new ScriptContextOptions
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

    private static LoadedTable LoadedTable(
        string alias,
        LoadedTableKind kind)
    {
        return new LoadedTable
        {
            Name = new ClickHouseTableName
            {
                Table = $"physical_{alias}"
            },
            Alias = alias,
            Kind = kind,
            Fields =
            [
                Field("key", DataType.Text),
                Field("value", DataType.Text)
            ]
        };
    }

    private static LoadedTableField Field(
        string name,
        DataType dataType)
    {
        return new LoadedTableField
        {
            Name = name,
            DataType = dataType,
            CanBeNull = true
        };
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

        public IReadOnlyList<string> ColumnNames { get; init; } = ["id", "name"];

        public IReadOnlyList<object?> RowValues { get; init; } = [1, "Moscow"];

        public IReadOnlyList<IReadOnlyList<object?>>? Rows { get; init; }

        public ValueTask<LoadFromSource> ResolveAsync(
            LoadStatement statement,
            ScriptContext context,
            CancellationToken cancellationToken = default)
        {
            ResolveCalls++;
            return ValueTask.FromResult<LoadFromSource>(new ReaderLoadFromSource
            {
                RequiresBuffer = false,
                OpenReaderAsync = _ => ValueTask.FromResult<DbDataReader>(CreateReader())
            });
        }

        private DbDataReader CreateReader()
        {
            var table = new DataTable();
            for (var ordinal = 0; ordinal < ColumnNames.Count; ordinal++)
            {
                table.Columns.Add(ColumnNames[ordinal], RowValues[ordinal]?.GetType() ?? typeof(string));
            }

            foreach (var row in Rows ?? [RowValues])
            {
                table.Rows.Add(row.ToArray());
            }

            return table.CreateDataReader();
        }
    }

    private sealed class TestLoadStatementExecutor : LoadStatementExecutor
    {
        public TestLoadStatementExecutor()
        {
            TempTableMaterializer = new TestTempTableMaterializer(this);
        }

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

        public bool ThrowOnSecondMaterialize { get; init; }

        public long FinalRowCount { get; init; }

        public TimeSpan? FinalMaterializeDelay { get; init; }

        public List<object[]> Rows { get; } = [];

        protected override async ValueTask<long> MaterializeFinalTableAsync(
            ScriptContext context,
            LoadStatement statement,
            string querySql,
            ClickHouseTableName finalTable,
            CancellationToken cancellationToken)
        {
            MaterializeCalls++;
            QuerySql = querySql;
            FinalTableName = finalTable;
            if (ThrowOnMaterialize || (ThrowOnSecondMaterialize && MaterializeCalls == 2))
            {
                throw new InvalidOperationException("materialize failed");
            }

            if (FinalMaterializeDelay is not null)
            {
                await Task.Delay(FinalMaterializeDelay.Value, cancellationToken).ConfigureAwait(false);
            }

            return FinalRowCount;
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

        private sealed class TestTempTableMaterializer : TempTableMaterializer
        {
            private readonly TestLoadStatementExecutor owner;

            public TestTempTableMaterializer(TestLoadStatementExecutor owner)
            {
                this.owner = owner;
            }

            protected override async ValueTask<long> WriteTempTableAsync(
                ScriptContext context,
                SourceRowsProgressDataReader reader,
                ClickHouseTableName tableName,
                CancellationToken cancellationToken)
            {
                owner.WriteCalls++;
                owner.TableName = tableName;

                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var values = new object[reader.FieldCount];
                    reader.GetValues(values);
                    owner.Rows.Add(values);
                }

                return owner.Rows.Count;
            }

            protected override ValueTask DropTempTableAsync(
                ScriptContext context,
                ClickHouseTableName tempTable,
                CancellationToken cancellationToken)
            {
                owner.DropCalls++;
                owner.DropTableName = tempTable;
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class RecordingTemporaryCleanupExecutor : TemporaryLoadedTableCleanupExecutor
    {
        public int ExecuteCalls { get; private set; }

        public int BestEffortCalls { get; private set; }

        public List<string> CleanedAliases { get; } = [];

        public override ValueTask ExecuteAsync(
            ScriptContext context,
            CancellationToken cancellationToken = default)
        {
            ExecuteCalls++;
            Clean(context);
            return ValueTask.CompletedTask;
        }

        public override ValueTask ExecuteBestEffortAsync(ScriptContext context)
        {
            BestEffortCalls++;
            Clean(context);
            return ValueTask.CompletedTask;
        }

        private void Clean(ScriptContext context)
        {
            foreach (var table in context.LoadedTables
                         .Where(static table => table.Kind == LoadedTableKind.Temp)
                         .ToArray())
            {
                CleanedAliases.Add(table.Alias!);
                context.RemoveLoadedTable(table);
            }
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

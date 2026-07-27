using System.Data;
using System.Data.Common;
using Loader.Core.Decorators;
using Loader.Core.Sources;
using Loader.Core.Writers.ClickHouse;
using Loader.Lang.Expressions;
using Loader.Lang.Statements;
using Loader.Script.Execution;
using Microsoft.Extensions.Logging.Abstractions;

namespace Loader.Script.Tests;

public sealed class LoadStatementExecutorTests
{
    [Test]
    public async Task Load_temp_table_resolves_source_normalizes_physical_columns_and_writes_temp_table()
    {
        var providerResolver = new FakeProviderResolver();
        var executor = new TestLoadStatementExecutor
        {
            ProviderResolver = providerResolver,
            TempTablePrefix = "tmp_"
        };
        var statement = new LoadStatement
        {
            TableName = "orders",
            Fields = null,
            Source = "orders.csv",
            Options = [],
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
            ProviderResolver = providerResolver,
            TempTablePrefix = "tmp_",
            FinalTablePrefix = "final_"
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
                    Expression = Expr.Parse("name").Value
                }
            ],
            Source = "orders.csv",
            Options = [],
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
            Limit = 10,
            Offset = 1
        };

        var loadedTable = await executor.ExecuteAsync(context, statement);

        await Assert.That(executor.WriteCalls).IsEqualTo(1);
        await Assert.That(executor.MaterializeCalls).IsEqualTo(1);
        await Assert.That(executor.DropCalls).IsEqualTo(1);
        await Assert.That(executor.DropTableName!.Table).IsEqualTo(executor.TableName!.Table);
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
    public async Task Execute_load_drops_temp_table_when_final_materialization_fails()
    {
        var executor = new TestLoadStatementExecutor
        {
            ProviderResolver = new FakeProviderResolver(),
            TempTablePrefix = "tmp_",
            ThrowOnMaterialize = true
        };
        var context = CreateContext();
        var statement = new LoadStatement
        {
            TableName = "orders",
            Fields = null,
            Source = "orders.csv",
            Options = [],
            Where = null,
            GroupBy = null,
            OrderBy = null
        };

        await Assert.That(async () => await executor.ExecuteAsync(context, statement))
            .ThrowsExactly<InvalidOperationException>()
            .WithMessage("materialize failed");

        await Assert.That(executor.WriteCalls).IsEqualTo(1);
        await Assert.That(executor.MaterializeCalls).IsEqualTo(1);
        await Assert.That(executor.DropCalls).IsEqualTo(1);
        await Assert.That(executor.DropTableName!.Table).IsEqualTo(executor.TableName!.Table);
        await Assert.That(context.LoadedTables).IsEmpty();
    }

    private static ScriptContext CreateContext()
    {
        return new ScriptContext
        {
            FileStorage = new StubFileSource(),
            TargetConnectionString = "Host=localhost",
            Logger = NullLogger.Instance
        };
    }

    private sealed class FakeProviderResolver : ILoadProviderResolver
    {
        public int ResolveCalls { get; private set; }

        public ValueTask<LoadProviderSource> ResolveAsync(
            LoadStatement statement,
            ScriptContext context,
            CancellationToken cancellationToken = default)
        {
            ResolveCalls++;
            return ValueTask.FromResult(new LoadProviderSource
            {
                Kind = "fake",
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

        public string? QuerySql { get; private set; }

        public bool ThrowOnMaterialize { get; init; }

        public List<object[]> Rows { get; } = [];

        protected override async ValueTask WriteTempTableAsync(
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
        }

        protected override ValueTask MaterializeFinalTableAsync(
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

            return ValueTask.CompletedTask;
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
    }

    private sealed class StubFileSource : IFileSource
    {
        public Stream OpenRead(string fileName)
        {
            return new MemoryStream();
        }
    }
}

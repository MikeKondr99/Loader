using System.Data;
using System.Data.Common;
using Loader.Core.Decorators;
using Loader.Core.Sources;
using Loader.Core.Writers.ClickHouse;
using Loader.Lang.Statements;
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

        var result = await executor.LoadTempTableAsync(CreateContext(), statement);

        await Assert.That(providerResolver.ResolveCalls).IsEqualTo(1);
        await Assert.That(executor.WriteCalls).IsEqualTo(1);
        await Assert.That(result.TableName.Table).StartsWith("tmp_orders_");
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
    }

    private sealed class StubFileSource : IFileSource
    {
        public Stream OpenRead(string fileName)
        {
            return new MemoryStream();
        }
    }
}

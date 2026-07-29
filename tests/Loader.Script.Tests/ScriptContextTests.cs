using Loader.Core.Sources;
using Loader.Core.Writers.ClickHouse;
using Microsoft.Extensions.Logging.Abstractions;

namespace Loader.Script.Tests;

public sealed class ScriptContextTests
{
    [Test]
    public async Task Context_stores_file_storage_and_target_connection_string()
    {
        var fileStorage = new StubFileSource();
        var context = new ScriptContext
        {
            FileStorage = fileStorage,
            TargetConnectionString = "Host=localhost;Database=loader",
            Logger = NullLogger.Instance
        };

        await Assert.That(context.FileStorage).IsSameReferenceAs(fileStorage);
        await Assert.That(context.TargetConnectionString).IsEqualTo("Host=localhost;Database=loader");
        await Assert.That(context.Logger).IsSameReferenceAs(NullLogger.Instance);
        await Assert.That(context.LoadedTables).IsEmpty();
    }

    [Test]
    public async Task Context_accumulates_loaded_tables_in_order()
    {
        var context = CreateContext();

        context.AddLoadedTable(new LoadedTable
        {
            Name = Table("physical_orders"),
            Alias = "orders",
            RowCount = 10,
            Fields = []
        });
        context.AddLoadedTable(new LoadedTable
        {
            Name = Table("physical_generated"),
            Alias = null,
            Fields = []
        });

        await Assert.That(context.LoadedTables).Count().IsEqualTo(2);
        await Assert.That(context.LoadedTables[0].Name.Table).IsEqualTo("physical_orders");
        await Assert.That(context.LoadedTables[0].Alias).IsEqualTo("orders");
        await Assert.That(context.LoadedTables[0].RowCount).IsEqualTo(10);
        await Assert.That(context.LoadedTables[1].Alias).IsNull();
    }

    [Test]
    public async Task Context_resolves_exactly_one_loaded_table_by_alias()
    {
        var context = CreateContext();
        var orders = new LoadedTable
        {
            Name = Table("physical_orders"),
            Alias = "Orders",
            Fields = []
        };
        context.AddLoadedTable(orders);

        await Assert.That(context.GetLoadedTable("Orders")).IsSameReferenceAs(orders);
        await Assert.That(() => context.GetLoadedTable("orders"))
            .ThrowsExactly<QueryResolutionException>();
    }

    [Test]
    public async Task Context_rejects_ambiguous_loaded_table_alias()
    {
        var context = CreateContext();
        context.AddLoadedTable(new LoadedTable
        {
            Name = Table("physical_orders_1"),
            Alias = "Orders",
            Fields = []
        });
        context.AddLoadedTable(new LoadedTable
        {
            Name = Table("physical_orders_2"),
            Alias = "Orders",
            Fields = []
        });

        await Assert.That(() => context.GetLoadedTable("Orders"))
            .ThrowsExactly<QueryResolutionException>()
            .WithMessageContaining("неоднозначно");
    }

    private static ClickHouseTableName Table(string name)
    {
        return new ClickHouseTableName
        {
            Table = name
        };
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

    private sealed class StubFileSource : IFileSource
    {
        public Stream OpenRead(string fileName)
        {
            return new MemoryStream();
        }
    }
}

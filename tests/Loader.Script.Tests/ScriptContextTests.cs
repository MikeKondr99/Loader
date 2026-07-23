using Loader.Core.Sources;

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
            TargetConnectionString = "Host=localhost;Database=loader"
        };

        await Assert.That(context.FileStorage).IsSameReferenceAs(fileStorage);
        await Assert.That(context.TargetConnectionString).IsEqualTo("Host=localhost;Database=loader");
        await Assert.That(context.LoadedTables).IsEmpty();
    }

    [Test]
    public async Task Context_accumulates_loaded_tables_in_order()
    {
        var context = CreateContext();

        context.AddLoadedTable(new LoadedTable
        {
            Name = "orders",
            RowCount = 10,
            Fields = []
        });
        context.AddLoadedTable(new LoadedTable
        {
            Name = null,
            Fields = []
        });

        await Assert.That(context.LoadedTables).Count().IsEqualTo(2);
        await Assert.That(context.LoadedTables[0].Name).IsEqualTo("orders");
        await Assert.That(context.LoadedTables[0].RowCount).IsEqualTo(10);
        await Assert.That(context.LoadedTables[1].Name).IsNull();
    }

    private static ScriptContext CreateContext()
    {
        return new ScriptContext
        {
            FileStorage = new StubFileSource(),
            TargetConnectionString = "Host=localhost"
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

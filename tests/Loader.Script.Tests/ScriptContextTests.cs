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
    public async Task Context_accumulates_loaded_table_names_in_order()
    {
        var context = CreateContext();

        context.AddLoadedTable("orders");
        context.AddLoadedTable("customers");

        await Assert.That(context.LoadedTables).Count().IsEqualTo(2);
        await Assert.That(context.LoadedTables[0]).IsEqualTo("orders");
        await Assert.That(context.LoadedTables[1]).IsEqualTo("customers");
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Context_rejects_empty_loaded_table_name(string tableName)
    {
        var context = CreateContext();

        await Assert.That(() => context.AddLoadedTable(tableName))
            .ThrowsExactly<ArgumentException>();
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

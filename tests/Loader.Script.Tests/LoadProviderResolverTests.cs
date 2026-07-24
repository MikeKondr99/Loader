using Loader.Core.Sources;
using Loader.Lang.Expressions;
using Loader.Lang.Statements;
using Microsoft.Extensions.Logging.Abstractions;

namespace Loader.Script.Tests;

public sealed class LoadProviderResolverTests
{
    [Test]
    public async Task Resolve_uses_file_extension_when_provider_marker_is_absent()
    {
        var resolver = new LoadProviderResolver();

        var source = await resolver.ResolveAsync(
            CreateStatement("orders.csv"),
            CreateContext());

        await Assert.That(source.Kind).IsEqualTo("csv");
        await Assert.That(source.RequiresBuffer).IsFalse();
    }

    [Test]
    public async Task Resolve_uses_database_provider_marker_and_table_option()
    {
        var resolver = new LoadProviderResolver();

        var source = await resolver.ResolveAsync(
            CreateStatement(
                "Host=localhost;Database=db",
                [
                    Marker("postgres"),
                    Option("table", "public.orders")
                ]),
            CreateContext());

        await Assert.That(source.Kind).IsEqualTo("postgres");
        await Assert.That(source.RequiresBuffer).IsFalse();
    }

    [Test]
    public async Task Resolve_rejects_unknown_source_without_provider_marker()
    {
        var resolver = new LoadProviderResolver();

        await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement("orders.unknown"),
                CreateContext()))
            .ThrowsExactly<InvalidOperationException>();
    }

    private static LoadStatement CreateStatement(string source, List<LoadOption>? options = null)
    {
        return new LoadStatement
        {
            TableName = null,
            Fields = null,
            Source = source,
            Options = options ?? [],
            Where = null,
            GroupBy = null,
            OrderBy = null
        };
    }

    private static LoadOption Marker(string name)
    {
        return new LoadOption
        {
            Name = name,
            Value = null
        };
    }

    private static LoadOption Option(string name, string value)
    {
        return new LoadOption
        {
            Name = name,
            Value = new StringLiteral(value)
        };
    }

    private static ScriptContext CreateContext()
    {
        return new ScriptContext
        {
            FileStorage = new StubFileSource(),
            TargetConnectionString = "Host=clickhouse",
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

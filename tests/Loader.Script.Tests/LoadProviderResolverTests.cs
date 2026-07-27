using Loader.Core.Exceptions;
using Loader.Core.Sources;
using Loader.Lang.Expressions;
using Loader.Lang.Statements;
using Loader.Script.Execution;
using Microsoft.Extensions.Logging.Abstractions;

namespace Loader.Script.Tests;

public sealed class LoadProviderResolverTests
{
    [Test]
    [DisplayName("Resolver выбирает файловый provider по расширению если marker не указан")]
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
    [DisplayName("Resolver выбирает DB provider по marker и table option")]
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
    [DisplayName("Resolver выбирает Hive provider по marker и table option")]
    public async Task Resolve_uses_hive_provider_marker_and_table_option()
    {
        var resolver = new LoadProviderResolver();

        var source = await resolver.ResolveAsync(
            CreateStatement(
                "Driver={Hive};Host=localhost;Port=10000;Schema=default",
                [
                    Marker("hive"),
                    Option("table", "default.orders")
                ]),
            CreateContext());

        await Assert.That(source.Kind).IsEqualTo("hive");
        await Assert.That(source.RequiresBuffer).IsTrue();
    }

    [Test]
    [DisplayName("Resolver для Hive поддерживает алиасы provider marker")]
    [Arguments("hive")]
    [Arguments("apachehive")]
    [Arguments("apache-hive")]
    public async Task Resolve_uses_hive_provider_aliases(string providerMarker)
    {
        var resolver = new LoadProviderResolver();

        var source = await resolver.ResolveAsync(
            CreateStatement(
                "Driver={Hive};Host=localhost;Port=10000;Schema=default",
                [
                    Marker(providerMarker),
                    Option("table", "analytics.orders")
                ]),
            CreateContext());

        await Assert.That(source.Kind).IsEqualTo("hive");
        await Assert.That(source.RequiresBuffer).IsTrue();
    }

    [Test]
    [DisplayName("Resolver для Hive читает marker без учета регистра")]
    public async Task Resolve_uses_hive_provider_marker_case_insensitive()
    {
        var resolver = new LoadProviderResolver();

        var source = await resolver.ResolveAsync(
            CreateStatement(
                "Driver={Hive};Host=localhost;Port=10000;Schema=default",
                [
                    Marker("HiVe"),
                    Option("table", "default.orders")
                ]),
            CreateContext());

        await Assert.That(source.Kind).IsEqualTo("hive");
        await Assert.That(source.RequiresBuffer).IsTrue();
    }

    [Test]
    [DisplayName("Resolver для Hive требует table option")]
    public async Task Resolve_rejects_hive_provider_without_table_option()
    {
        var resolver = new LoadProviderResolver();

        await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement(
                    "Driver={Hive};Host=localhost;Port=10000;Schema=default",
                    [Marker("hive")]),
                CreateContext()))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    [DisplayName("Resolver для Hive отклоняет небезопасное имя таблицы")]
    public async Task Resolve_rejects_hive_provider_with_unsafe_table_name()
    {
        var resolver = new LoadProviderResolver();

        await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement(
                    "Driver={Hive};Host=localhost;Port=10000;Schema=default",
                    [
                        Marker("hive"),
                        Option("table", "default.orders;drop_table")
                    ]),
                CreateContext()))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    [DisplayName("Hive provider ошибку ODBC соединения оборачивает в DbExecutionException")]
    [ParallelLimiter<ApacheHiveParallelLimit>]
    public async Task Hive_provider_wraps_odbc_connection_error()
    {
        var resolver = new LoadProviderResolver();
        var source = await resolver.ResolveAsync(
            CreateStatement(
                "Driver={__loader_missing_hive_driver__};Host=localhost;Port=10000;Schema=default",
                [
                    Marker("hive"),
                    Option("table", "default.orders")
                ]),
            CreateContext());

        await Assert.That(async () => await source.OpenReaderAsync(CancellationToken.None))
            .ThrowsExactly<DbExecutionException>()
            .WithMessage("Database query failed for provider 'hive': SELECT * FROM default.orders");
    }

    [Test]
    [DisplayName("Resolver отклоняет неизвестный source без provider marker")]
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

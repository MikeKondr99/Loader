using Loader.Core.Exceptions;
using Loader.Core.Sources;
using Loader.Lang;
using Loader.Lang.Expressions;
using Loader.Lang.Statements;
using Loader.Script.Execution;
using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Assertions.Enums;

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
        var fromSpan = Span(2, 5, 9);

        var exception = await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement(
                    "Driver={Hive};Host=localhost;Port=10000;Schema=default",
                    [Marker("hive")],
                    fromSpan),
                CreateContext()))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Stage).IsEqualTo(LoadScriptStage.ProviderResolution);
        await Assert.That(exception.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Span).IsEqualTo(fromSpan);
        await Assert.That(exception.Errors[0].Message).Contains("table='schema.table'");
    }

    [Test]
    [DisplayName("Resolver для Hive отклоняет небезопасное имя таблицы")]
    public async Task Resolve_rejects_hive_provider_with_unsafe_table_name()
    {
        var resolver = new LoadProviderResolver();
        var tableSpan = Span(3, 10, 41);

        var exception = await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement(
                    "Driver={Hive};Host=localhost;Port=10000;Schema=default",
                    [
                        Marker("hive"),
                        Option("table", "default.orders;drop_table", tableSpan)
                    ]),
                CreateContext()))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Span).IsEqualTo(tableSpan);
        await Assert.That(exception.Errors[0].Message).Contains("не поддерживается");
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
        var fromSpan = Span(4, 1, 5);

        var exception = await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement("orders.unknown", fromSpan: fromSpan),
                CreateContext()))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Span).IsEqualTo(fromSpan);
        await Assert.That(exception.Errors[0].Message).Contains("Нужно указать provider marker");
    }

    [Test]
    [DisplayName("Resolver указывает span option если header не boolean")]
    public async Task Resolve_rejects_header_option_with_non_boolean_value()
    {
        var resolver = new LoadProviderResolver();
        var headerSpan = Span(5, 20, 32);

        var exception = await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement(
                    "orders.csv",
                    [Option("header", "yes", headerSpan)]),
                CreateContext()))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Span).IsEqualTo(headerSpan);
        await Assert.That(exception.Errors[0].Message).Contains("header");
        await Assert.That(exception.Errors[0].Message).Contains("true или false");
    }

    [Test]
    [DisplayName("Resolver возвращает несколько ошибок provider options")]
    public async Task Resolve_returns_multiple_provider_option_errors()
    {
        var resolver = new LoadProviderResolver();
        var fromSpan = Span(6, 1, 5);
        var csvSpan = Span(6, 48, 51);

        var exception = await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement(
                    "Host=localhost;Database=db",
                    [
                        Marker("postgres"),
                        Marker("csv", csvSpan)
                    ],
                    fromSpan),
                CreateContext()))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(2);
        await Assert.That(exception.Errors.Select(static error => error.Span).ToArray())
            .IsEquivalentTo([csvSpan, fromSpan], CollectionOrdering.Matching);
    }

    private static LoadStatement CreateStatement(
        string source,
        List<LoadOption>? options = null,
        LangSpan? fromSpan = null)
    {
        return new LoadStatement
        {
            TableName = null,
            Fields = null,
            FromSpan = fromSpan ?? Span(),
            SourcePart = new SourcePart
            {
                Value = source,
                Span = Span()
            },
            Options = options ?? [],
            Where = null,
            GroupBy = null,
            OrderBy = null
        };
    }

    private static LoadOption Marker(string name)
    {
        return Marker(name, Span());
    }

    private static LoadOption Marker(string name, LangSpan span)
    {
        return new LoadOption
        {
            Name = name,
            Span = span,
            Value = null
        };
    }

    private static LoadOption Option(string name, string value)
    {
        return Option(name, value, Span());
    }

    private static LoadOption Option(string name, string value, LangSpan span)
    {
        return new LoadOption
        {
            Name = name,
            Span = span,
            Value = new StringLiteral(value)
        };
    }

    private static LangSpan Span()
    {
        return new LangSpan(1, 1, 1, 1);
    }

    private static LangSpan Span(uint row, uint startColumn, uint endColumn)
    {
        return new LangSpan(row, startColumn, row, endColumn);
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

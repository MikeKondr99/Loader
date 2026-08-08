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
    [DisplayName("Resolver выбирает файловый provider по имени SourceCall")]
    public async Task Resolve_uses_file_provider_name()
    {
        var resolver = new LoadProviderResolver();

        var source = await resolver.ResolveAsync(
            CreateStatement("Csv", [Option("path", "orders.csv")]),
            CreateContext());

        await Assert.That(source.Kind).IsEqualTo("csv");
        await Assert.That(source.RequiresBuffer).IsFalse();
    }

    [Test]
    [DisplayName("Resolver выбирает DB provider по имени SourceCall и SQL инструкции")]
    public async Task Resolve_uses_database_provider_name_and_sql()
    {
        var resolver = new LoadProviderResolver();

        var source = await resolver.ResolveAsync(
            CreateStatement(
                "Postgres",
                [Option("connection", "Host=localhost;Database=db")],
                sql: "SELECT * FROM public.orders"),
            CreateContext());

        await Assert.That(source.Kind).IsEqualTo("postgres");
        await Assert.That(source.RequiresBuffer).IsFalse();
    }

    [Test]
    [DisplayName("Resolver выбирает Hive provider по имени SourceCall и SQL инструкции")]
    public async Task Resolve_uses_hive_provider_name_and_sql()
    {
        var resolver = new LoadProviderResolver();

        var source = await resolver.ResolveAsync(
            CreateStatement(
                "Hive",
                [Option("connection", "Driver={Hive};Host=localhost;Port=10000;Schema=default")],
                sql: "SELECT * FROM default.orders"),
            CreateContext());

        await Assert.That(source.Kind).IsEqualTo("hive");
        await Assert.That(source.RequiresBuffer).IsTrue();
    }

    [Test]
    [DisplayName("Resolver для Hive отклоняет aliases provider name")]
    [Arguments("apachehive")]
    [Arguments("apache-hive")]
    public async Task Resolve_rejects_hive_provider_aliases(string providerName)
    {
        var resolver = new LoadProviderResolver();
        var providerSpan = Span(4, 12, 20);

        var exception = await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement(
                    providerName,
                    [Option("connection", "Driver={Hive};Host=localhost;Port=10000;Schema=default")],
                    providerSpan: providerSpan,
                    sql: "SELECT * FROM analytics.orders"),
                CreateContext()))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Span).IsEqualTo(providerSpan);
        await Assert.That(exception.Errors[0].Message).Contains("не поддерживается");
    }

    [Test]
    [DisplayName("Resolver для Hive читает provider name без учета регистра")]
    public async Task Resolve_uses_hive_provider_name_case_insensitive()
    {
        var resolver = new LoadProviderResolver();

        var source = await resolver.ResolveAsync(
            CreateStatement(
                "HiVe",
                [Option("connection", "Driver={Hive};Host=localhost;Port=10000;Schema=default")],
                sql: "SELECT * FROM default.orders"),
            CreateContext());

        await Assert.That(source.Kind).IsEqualTo("hive");
        await Assert.That(source.RequiresBuffer).IsTrue();
    }

    [Test]
    [DisplayName("Resolver для Hive требует SQL инструкции")]
    public async Task Resolve_rejects_hive_provider_without_sql()
    {
        var resolver = new LoadProviderResolver();
        var fromSpan = Span(2, 5, 9);

        var exception = await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement(
                    "Hive",
                    [Option("connection", "Driver={Hive};Host=localhost;Port=10000;Schema=default")],
                    fromSpan),
                CreateContext()))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Stage).IsEqualTo(LoadScriptStage.ProviderResolution);
        await Assert.That(exception.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Span).IsEqualTo(fromSpan);
        await Assert.That(exception.Errors[0].Message).Contains("требуется SQL после FROM");
    }

    [Test]
    [DisplayName("Resolver для DB provider игнорирует table option если SQL указан")]
    public async Task Resolve_ignores_database_provider_table_option_when_sql_is_present()
    {
        var resolver = new LoadProviderResolver();

        var source = await resolver.ResolveAsync(
            CreateStatement(
                "Hive",
                [
                    Option("connection", "Driver={Hive};Host=localhost;Port=10000;Schema=default"),
                    Option("table", "default.orders")
                ],
                sql: "SELECT * FROM default.orders"),
            CreateContext());

        await Assert.That(source.Kind).IsEqualTo("hive");
        await Assert.That(source.RequiresBuffer).IsTrue();
    }

    [Test]
    [DisplayName("Hive provider ошибку ODBC соединения оборачивает в DbExecutionException")]
    [ParallelLimiter<ApacheHiveParallelLimit>]
    public async Task Hive_provider_wraps_odbc_connection_error()
    {
        var resolver = new LoadProviderResolver();
        var source = await resolver.ResolveAsync(
            CreateStatement(
                "Hive",
                [Option("connection", "Driver={__loader_missing_hive_driver__};Host=localhost;Port=10000;Schema=default")],
                sql: "SELECT * FROM default.orders"),
            CreateContext());

        await Assert.That(async () => await source.OpenReaderAsync(CancellationToken.None))
            .ThrowsExactly<DbExecutionException>()
            .WithMessage("Database query failed for provider 'hive': SELECT * FROM default.orders");
    }

    [Test]
    [DisplayName("Resolver отклоняет неизвестный provider name и подсказывает ближайший")]
    public async Task Resolve_rejects_unknown_provider_name_with_suggestion()
    {
        var resolver = new LoadProviderResolver();
        var providerSpan = Span(4, 12, 20);

        var exception = await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement("Postgre", [Option("connection", "Host=localhost")], providerSpan: providerSpan),
                CreateContext()))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Span).IsEqualTo(providerSpan);
        await Assert.That(exception.Errors[0].Message).Contains("не поддерживается");
        await Assert.That(exception.Errors[0].Message).Contains("Возможно вы имели в виду 'Postgres'");
    }

    [Test]
    [DisplayName("Resolver отклоняет SQL инструкцию для файлового provider")]
    public async Task Resolve_rejects_sql_for_file_provider()
    {
        var resolver = new LoadProviderResolver();

        var exception = await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement(
                    "Csv",
                    [Option("path", "orders.csv")],
                    sql: "SELECT * FROM orders"),
                CreateContext()))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Message).Contains("не поддерживает SQL");
    }

    [Test]
    [DisplayName("Resolver отклоняет пустую SQL инструкцию для DB provider")]
    public async Task Resolve_rejects_empty_sql_for_database_provider()
    {
        var resolver = new LoadProviderResolver();

        var exception = await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement(
                    "Postgres",
                    [Option("connection", "Host=localhost;Database=db")],
                    sql: "   "),
                CreateContext()))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Message).Contains("SQL не должен быть пустым");
    }

    [Test]
    [DisplayName("Resolver указывает span option если header не boolean")]
    public async Task Resolve_rejects_header_option_with_non_boolean_value()
    {
        var resolver = new LoadProviderResolver();
        var headerSpan = Span(5, 20, 32);

        var exception = await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement(
                    "Csv",
                    [
                        Option("path", "orders.csv"),
                        Option("header", "yes", headerSpan)
                    ]),
                CreateContext()))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Span).IsEqualTo(headerSpan);
        await Assert.That(exception.Errors[0].Message).Contains("header");
        await Assert.That(exception.Errors[0].Message).Contains("true или false");
    }

    [Test]
    [DisplayName("Resolver указывает span option если delimiter не один символ")]
    public async Task Resolve_rejects_delimiter_option_with_more_than_one_character()
    {
        var resolver = new LoadProviderResolver();
        var delimiterSpan = Span(5, 20, 35);

        var exception = await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement(
                    "Csv",
                    [
                        Option("path", "orders.csv"),
                        Option("delimiter", "||", delimiterSpan)
                    ]),
                CreateContext()))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Span).IsEqualTo(delimiterSpan);
        await Assert.That(exception.Errors[0].Message).Contains("delimiter");
        await Assert.That(exception.Errors[0].Message).Contains("один символ");
    }

    [Test]
    [DisplayName("Resolver возвращает ошибки по повторяющимся options")]
    public async Task Resolve_rejects_duplicate_named_options()
    {
        var resolver = new LoadProviderResolver();
        var duplicateSpan = Span(5, 35, 47);

        var exception = await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement(
                    "Csv",
                    [
                        Option("path", "orders.csv"),
                        Option("header", new BooleanLiteral(true), Span(5, 20, 31)),
                        Option("header", new BooleanLiteral(false), duplicateSpan)
                    ]),
                CreateContext()))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Span).IsEqualTo(duplicateSpan);
        await Assert.That(exception.Errors[0].Message).Contains("header");
        await Assert.That(exception.Errors[0].Message).Contains("несколько раз");
    }

    [Test]
    [DisplayName("Resolver JSON root читает массив внутри объекта")]
    public async Task Resolve_json_root_reads_array_inside_object()
    {
        var resolver = new LoadProviderResolver();

        var source = await resolver.ResolveAsync(
            CreateStatement(
                "Json",
                [
                    Option("path", "nested.json"),
                    Option("root", "response.items")
                ]),
            CreateContext(new StubFileSource("""
                {
                  "response": {
                    "items": [
                      { "id": 1, "city": "Moscow" },
                      { "id": 2, "city": "Berlin" }
                    ]
                  }
                }
                """)));

        await using var reader = await source.OpenReaderAsync(CancellationToken.None);

        await Assert.That(source.Kind).IsEqualTo("json");
        await Assert.That(reader.FieldCount).IsEqualTo(2);
        await Assert.That(reader.GetName(0)).IsEqualTo("id");
        await Assert.That(reader.GetName(1)).IsEqualTo("city");
        await Assert.That(await reader.ReadAsync()).IsTrue();
        await Assert.That(reader.GetValue(0)).IsEqualTo("1");
        await Assert.That(reader.GetValue(1)).IsEqualTo("Moscow");
    }

    [Test]
    [DisplayName("Resolver JSON root может указывать на массив внутри элемента массива")]
    public async Task Resolve_json_root_reads_array_inside_array_item()
    {
        var resolver = new LoadProviderResolver();

        var source = await resolver.ResolveAsync(
            CreateStatement(
                "Json",
                [
                    Option("path", "nested.json"),
                    Option("root", "blocks.1.items")
                ]),
            CreateContext(new StubFileSource("""
                {
                  "blocks": [
                    {
                      "ignored": true
                    },
                    {
                      "items": [
                        { "id": 10 },
                        { "id": 20 }
                      ]
                    }
                  ]
                }
                """)));

        await using var reader = await source.OpenReaderAsync(CancellationToken.None);

        await Assert.That(reader.FieldCount).IsEqualTo(1);
        await Assert.That(reader.GetName(0)).IsEqualTo("id");
        await Assert.That(await reader.ReadAsync()).IsTrue();
        await Assert.That(reader.GetValue(0)).IsEqualTo("10");
        await Assert.That(await reader.ReadAsync()).IsTrue();
        await Assert.That(reader.GetValue(0)).IsEqualTo("20");
    }

    [Test]
    [DisplayName("Resolver JSON root поддерживает индекс массива в пути")]
    public async Task Resolve_json_root_reads_array_index_path()
    {
        var resolver = new LoadProviderResolver();

        var source = await resolver.ResolveAsync(
            CreateStatement(
                "Json",
                [
                    Option("path", "tables.json"),
                    Option("root", "tables.0.data")
                ]),
            CreateContext(new StubFileSource("""
                {
                  "tables": [
                    {
                      "data": [
                        { "id": 1 }
                      ]
                    },
                    {
                      "data": [
                        { "id": 99 }
                      ]
                    }
                  ]
                }
                """)));

        await using var reader = await source.OpenReaderAsync(CancellationToken.None);

        await Assert.That(reader.FieldCount).IsEqualTo(1);
        await Assert.That(reader.GetName(0)).IsEqualTo("id");
        await Assert.That(await reader.ReadAsync()).IsTrue();
        await Assert.That(reader.GetValue(0)).IsEqualTo("1");
        await Assert.That(await reader.ReadAsync()).IsFalse();
    }

    [Test]
    [DisplayName("Resolver JSON root пустой строки отклоняет как provider option")]
    public async Task Resolve_json_rejects_empty_root_option()
    {
        var resolver = new LoadProviderResolver();
        var rootSpan = Span(5, 20, 27);

        var exception = await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement(
                    "Json",
                    [
                        Option("path", "orders.json"),
                        Option("root", string.Empty, rootSpan)
                    ]),
                CreateContext(new StubFileSource("[]"))))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Span).IsEqualTo(rootSpan);
        await Assert.That(exception.Errors[0].Message).Contains("root");
    }

    [Test]
    [DisplayName("Resolver JSON root должен быть строкой")]
    public async Task Resolve_json_rejects_non_string_root_option()
    {
        var resolver = new LoadProviderResolver();
        var rootSpan = Span(5, 20, 27);

        var exception = await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement(
                    "Json",
                    [
                        Option("path", "orders.json"),
                        Option("root", new IntegerLiteral(1), rootSpan)
                    ]),
                CreateContext(new StubFileSource("[]"))))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Span).IsEqualTo(rootSpan);
        await Assert.That(exception.Errors[0].Message).Contains("root");
        await Assert.That(exception.Errors[0].Message).Contains("строкой");
    }

    [Test]
    [DisplayName("Resolver JSON ошибку открытия файла оборачивает как ProviderResolution ошибку")]
    public async Task Resolve_json_wraps_file_open_error_as_provider_resolution()
    {
        var resolver = new LoadProviderResolver();
        var sourceCallSpan = Span(5, 10, 42);

        var exception = await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement(
                    "Json",
                    [Option("path", "missing.json")],
                    sourceCallSpan: sourceCallSpan),
                CreateContext(new ThrowingFileSource())))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Span).IsEqualTo(sourceCallSpan);
        await Assert.That(exception.Errors[0].Message).Contains("Не удалось подготовить provider 'Json'");
        await Assert.That(exception.InnerException).IsTypeOf<JsonFileOpenProviderException>();
    }

    [Test]
    [DisplayName("Resolver возвращает несколько ошибок provider options")]
    public async Task Resolve_returns_multiple_provider_option_errors()
    {
        var resolver = new LoadProviderResolver();
        var fromSpan = Span(6, 1, 5);
        var duplicateConnectionSpan = Span(6, 48, 51);

        var exception = await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement(
                    "Postgres",
                    [
                        Option("connection", "Host=localhost;Database=db", Span(6, 10, 46)),
                        Option("connection", "Host=localhost;Database=other", duplicateConnectionSpan)
                    ],
                    fromSpan),
                CreateContext()))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(2);
        await Assert.That(exception.Errors.Select(static error => error.Span).ToArray())
            .IsEquivalentTo([duplicateConnectionSpan, fromSpan], CollectionOrdering.Matching);
    }

    private static LoadStatement CreateStatement(
        string provider,
        List<LoadOption>? options = null,
        LangSpan? fromSpan = null,
        LangSpan? providerSpan = null,
        LangSpan? sourceCallSpan = null,
        string? sql = null)
    {
        return new LoadStatement
        {
            TableName = null,
            Fields = null,
            FromSpan = fromSpan ?? Span(),
            SourceCall = new LoadSourceCall
            {
                Name = provider,
                NameSpan = providerSpan ?? Span(),
                Options = options ?? [],
                Span = sourceCallSpan ?? Span()
            },
            SqlPart = sql is null
                ? null
                : new SqlPart
                {
                    Value = sql,
                    Span = Span()
                },
            Where = null,
            GroupBy = null,
            OrderBy = null
        };
    }

    private static LoadOption Option(string name, string value)
    {
        return Option(name, value, Span());
    }

    private static LoadOption Option(string name, string value, LangSpan span)
    {
        return Option(name, new StringLiteral(value), span);
    }

    private static LoadOption Option(string name, Literal value, LangSpan span)
    {
        return new LoadOption
        {
            Name = name,
            Span = span,
            Value = value
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

    private static ScriptContext CreateContext(IFileSource? fileSource = null)
    {
        return new ScriptContext
        {
            FileStorage = fileSource ?? new StubFileSource(),
            TargetConnectionString = "Host=clickhouse",
            Logger = NullLogger.Instance
        };
    }

    private sealed class StubFileSource : IFileSource
    {
        private readonly string content;

        public StubFileSource()
            : this(string.Empty)
        {
        }

        public StubFileSource(string content)
        {
            this.content = content;
        }

        public Stream OpenRead(string fileName)
        {
            return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        }
    }

    private sealed class ThrowingFileSource : IFileSource
    {
        public Stream OpenRead(string fileName)
        {
            throw new FileNotFoundException("missing", fileName);
        }
    }
}

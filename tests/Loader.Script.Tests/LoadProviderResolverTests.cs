using System.Data.Common;
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
    [DisplayName("Resolver выбирает ODBC provider по имени SourceCall и SQL инструкции")]
    public async Task Resolve_uses_odbc_provider_name_and_sql()
    {
        var resolver = new LoadProviderResolver();

        var source = await resolver.ResolveAsync(
            CreateStatement(
                "Odbc",
                [Option("connection", "Driver={ODBC Driver 18 for SQL Server};Server=localhost;Database=db")],
                sql: "SELECT * FROM dbo.orders"),
            CreateContext());

        await Assert.That(source.Kind).IsEqualTo("odbc");
        await Assert.That(source.RequiresBuffer).IsTrue();
    }

    [Test]
    [DisplayName("Resolver читает ODBC provider name без учета регистра")]
    public async Task Resolve_uses_odbc_provider_name_case_insensitive()
    {
        var resolver = new LoadProviderResolver();

        var source = await resolver.ResolveAsync(
            CreateStatement(
                "oDbC",
                [Option("connection", "Driver={PostgreSQL Unicode(x64)};Server=localhost;Database=db")],
                sql: "select * from public.orders"),
            CreateContext());

        await Assert.That(source.Kind).IsEqualTo("odbc");
        await Assert.That(source.RequiresBuffer).IsTrue();
    }

    [Test]
    [DisplayName("Resolver Numbers создает поток чисел от 0 до max включительно")]
    public async Task Resolve_numbers_reads_default_range()
    {
        var resolver = new LoadProviderResolver();

        var source = await resolver.ResolveAsync(
            CreateStatement("Numbers", [Option("max", 3)]),
            CreateContext());
        await using var reader = await source.OpenReaderAsync(CancellationToken.None);

        await Assert.That(source.Kind).IsEqualTo("numbers");
        await Assert.That(source.RequiresBuffer).IsFalse();
        await Assert.That(reader.FieldCount).IsEqualTo(1);
        await Assert.That(reader.GetName(0)).IsEqualTo("number");
        await Assert.That(reader.GetFieldType(0)).IsEqualTo(typeof(long));
        await Assert.That(await ReadNumbersAsync(reader))
            .IsEquivalentTo([0L, 1L, 2L, 3L], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Resolver Numbers учитывает min и step")]
    public async Task Resolve_numbers_reads_min_step_range()
    {
        var resolver = new LoadProviderResolver();

        var source = await resolver.ResolveAsync(
            CreateStatement(
                "Numbers",
                [
                    Option("min", 2),
                    Option("max", 8),
                    Option("step", 3)
                ]),
            CreateContext());
        await using var reader = await source.OpenReaderAsync(CancellationToken.None);

        await Assert.That(await ReadNumbersAsync(reader))
            .IsEquivalentTo([2L, 5L, 8L], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Resolver Numbers требует max option")]
    public async Task Resolve_numbers_rejects_missing_max()
    {
        var resolver = new LoadProviderResolver();
        var sourceCallSpan = Span(3, 10, 19);

        var exception = await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement("Numbers", sourceCallSpan: sourceCallSpan),
                CreateContext()))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Span).IsEqualTo(sourceCallSpan);
        await Assert.That(exception.Errors[0].Message).Contains("max=1000");
    }

    [Test]
    [DisplayName("Resolver Numbers требует integer max option")]
    public async Task Resolve_numbers_rejects_non_integer_max()
    {
        var resolver = new LoadProviderResolver();
        var maxSpan = Span(3, 18, 27);

        var exception = await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement("Numbers", [Option("max", "100", maxSpan)]),
                CreateContext()))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Span).IsEqualTo(maxSpan);
        await Assert.That(exception.Errors[0].Message).Contains("max");
        await Assert.That(exception.Errors[0].Message).Contains("целым числом");
    }

    [Test]
    [DisplayName("Resolver Numbers отклоняет step меньше или равный 0")]
    public async Task Resolve_numbers_rejects_zero_step()
    {
        var resolver = new LoadProviderResolver();
        var stepSpan = Span(3, 25, 31);

        var exception = await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement(
                    "Numbers",
                    [
                        Option("max", 10),
                        Option("step", 0, stepSpan)
                    ]),
                CreateContext()))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Span).IsEqualTo(stepSpan);
        await Assert.That(exception.Errors[0].Message).Contains("step");
    }

    [Test]
    [DisplayName("Resolver Numbers отклоняет max меньше min")]
    public async Task Resolve_numbers_rejects_max_less_than_min()
    {
        var resolver = new LoadProviderResolver();
        var maxSpan = Span(3, 18, 24);

        var exception = await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement(
                    "Numbers",
                    [
                        Option("min", 10),
                        Option("max", 5, maxSpan)
                    ]),
                CreateContext()))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Span).IsEqualTo(maxSpan);
        await Assert.That(exception.Errors[0].Message).Contains("max");
        await Assert.That(exception.Errors[0].Message).Contains("min");
    }

    [Test]
    [DisplayName("Resolver Numbers отклоняет SQL после FROM")]
    public async Task Resolve_numbers_rejects_sql()
    {
        var resolver = new LoadProviderResolver();
        var sqlSpan = Span(4, 1, 20);
        var statement = CreateStatement(
            "Numbers",
            [Option("max", 10)],
            sql: "SELECT 1") with
        {
            SqlPart = new SqlPart
            {
                Value = "SELECT 1",
                Span = sqlSpan
            }
        };

        var exception = await Assert.That(async () => await resolver.ResolveAsync(statement, CreateContext()))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Span).IsEqualTo(sqlSpan);
        await Assert.That(exception.Errors[0].Message).Contains("SQL");
    }

    [Test]
    [DisplayName("Resolver Inline возвращает reader с выведенными типами")]
    public async Task Resolve_inline_returns_reader_with_inferred_types()
    {
        var resolver = new LoadProviderResolver();
        var statement = ParseLoadStatement(
            "test: LOAD * FROM Inline(id, name, active, amount; 1, 'Mike', true, -10.5; -2, null, false, 0);");

        var source = await resolver.ResolveAsync(statement, CreateContext());
        await using var reader = await source.OpenReaderAsync(CancellationToken.None);

        await Assert.That(source.Kind).IsEqualTo("inline");
        await Assert.That(reader.FieldCount).IsEqualTo(4);
        await Assert.That(reader.GetName(0)).IsEqualTo("id");
        await Assert.That(reader.GetFieldType(0)).IsEqualTo(typeof(long));
        await Assert.That(reader.GetFieldType(1)).IsEqualTo(typeof(string));
        await Assert.That(reader.GetFieldType(2)).IsEqualTo(typeof(bool));
        await Assert.That(reader.GetFieldType(3)).IsEqualTo(typeof(double));
        await Assert.That(await reader.ReadAsync()).IsTrue();
        await Assert.That(reader.GetInt64(0)).IsEqualTo(1);
        await Assert.That(reader.GetString(1)).IsEqualTo("Mike");
        await Assert.That(reader.GetBoolean(2)).IsTrue();
        await Assert.That(reader.GetDouble(3)).IsEqualTo(-10.5);
        await Assert.That(await reader.ReadAsync()).IsTrue();
        await Assert.That(reader.GetInt64(0)).IsEqualTo(-2);
        await Assert.That(reader.IsDBNull(1)).IsTrue();
        await Assert.That(reader.GetBoolean(2)).IsFalse();
        await Assert.That(reader.GetDouble(3)).IsEqualTo(0.0);
    }

    [Test]
    [DisplayName("Resolver Inline выводит Number для колонки со смесью Integer и Number")]
    public async Task Resolve_inline_infers_number_for_integer_and_number_values()
    {
        var resolver = new LoadProviderResolver();
        var statement = ParseLoadStatement("test: LOAD * FROM Inline(a; 1; 2.0;);");

        var source = await resolver.ResolveAsync(statement, CreateContext());
        await using var reader = await source.OpenReaderAsync(CancellationToken.None);

        await Assert.That(reader.FieldCount).IsEqualTo(1);
        await Assert.That(reader.GetName(0)).IsEqualTo("a");
        await Assert.That(reader.GetFieldType(0)).IsEqualTo(typeof(double));
        await Assert.That(await reader.ReadAsync()).IsTrue();
        await Assert.That(reader.GetDouble(0)).IsEqualTo(1.0);
        await Assert.That(await reader.ReadAsync()).IsTrue();
        await Assert.That(reader.GetDouble(0)).IsEqualTo(2.0);
        await Assert.That(await reader.ReadAsync()).IsFalse();
    }

    [Test]
    [DisplayName("Resolver Inline сводит смешанные primitive-типы к Text")]
    [Arguments("Inline(a; 1; 'x';)", "1", "x")]
    [Arguments("Inline(a; true; 'x';)", "true", "x")]
    [Arguments("Inline(a; 2.5; 'x';)", "2.5", "x")]
    [Arguments("Inline(a; true; 1;)", "true", "1")]
    public async Task Resolve_inline_infers_text_for_incompatible_mixed_values(
        string inline,
        string expectedFirst,
        string expectedSecond)
    {
        var resolver = new LoadProviderResolver();
        var statement = ParseLoadStatement($"test: LOAD * FROM {inline};");

        var source = await resolver.ResolveAsync(statement, CreateContext());
        await using var reader = await source.OpenReaderAsync(CancellationToken.None);

        await Assert.That(reader.GetFieldType(0)).IsEqualTo(typeof(string));
        await Assert.That(await reader.ReadAsync()).IsTrue();
        await Assert.That(reader.GetString(0)).IsEqualTo(expectedFirst);
        await Assert.That(await reader.ReadAsync()).IsTrue();
        await Assert.That(reader.GetString(0)).IsEqualTo(expectedSecond);
    }

    [Test]
    [DisplayName("Resolver Inline учитывает null при выводе типа и nullable schema")]
    [Arguments("Inline(a; null; 1;)", typeof(long), true)]
    [Arguments("Inline(a; null; 2.5;)", typeof(double), true)]
    [Arguments("Inline(a; null; true;)", typeof(bool), true)]
    [Arguments("Inline(a; null; 'x';)", typeof(string), true)]
    [Arguments("Inline(a; null; null;)", typeof(string), true)]
    public async Task Resolve_inline_infers_type_and_nullable_for_null_mixed_values(
        string inline,
        Type expectedType,
        bool expectedNullable)
    {
        var resolver = new LoadProviderResolver();
        var statement = ParseLoadStatement($"test: LOAD * FROM {inline};");

        var source = await resolver.ResolveAsync(statement, CreateContext());
        await using var reader = await source.OpenReaderAsync(CancellationToken.None);
        var schema = reader.GetSchemaTable();

        await Assert.That(schema).IsNotNull();
        await Assert.That(reader.GetFieldType(0)).IsEqualTo(expectedType);
        await Assert.That((bool)schema!.Rows[0][SchemaTableColumn.AllowDBNull]).IsEqualTo(expectedNullable);
        await Assert.That(await reader.ReadAsync()).IsTrue();
        await Assert.That(reader.IsDBNull(0)).IsTrue();
    }

    [Test]
    [DisplayName("Resolver Inline отклоняет row другой ширины")]
    public async Task Resolve_inline_rejects_row_width_mismatch()
    {
        var resolver = new LoadProviderResolver();
        var statement = ParseLoadStatement("test: LOAD * FROM Inline(id, name; 1);");

        var exception = await Assert.That(async () => await resolver.ResolveAsync(statement, CreateContext()))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Message).Contains("ожидалось 2");
    }

    [Test]
    [DisplayName("Resolver Inline отклоняет LOAD transformations")]
    [Arguments("WHERE id > 0", "WHERE")]
    [Arguments("GROUP BY id", "GROUP BY")]
    [Arguments("ORDER BY id", "ORDER BY")]
    [Arguments("LIMIT 1", "LIMIT")]
    public async Task Resolve_inline_rejects_load_transformations(string clause, string expectedClause)
    {
        var resolver = new LoadProviderResolver();
        var statement = ParseLoadStatement($"test: LOAD * FROM Inline(id; 1) {clause};");

        var exception = await Assert.That(async () => await resolver.ResolveAsync(statement, CreateContext()))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Message).Contains(expectedClause);
        await Assert.That(exception.Errors[0].Message).Contains("отдельный LOAD");
        await Assert.That(exception.Errors[0].Span).IsEqualTo(expectedClause switch
        {
            "WHERE" => statement.WhereSpan,
            "GROUP BY" => statement.GroupBySpan,
            "ORDER BY" => statement.OrderBySpan,
            "LIMIT" => statement.LimitPart!.Span,
            _ => throw new ArgumentOutOfRangeException(nameof(expectedClause), expectedClause, null)
        });
    }

    [Test]
    [DisplayName("Resolver Inline отклоняет OFFSET с span на keyword")]
    public async Task Resolve_inline_rejects_offset_with_keyword_span()
    {
        var resolver = new LoadProviderResolver();
        var statement = ParseLoadStatement("test: LOAD * FROM Inline(id; 1) LIMIT 1 OFFSET 2;");

        var exception = await Assert.That(async () => await resolver.ResolveAsync(statement, CreateContext()))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(2);
        await Assert.That(exception.Errors[1].Message).Contains("OFFSET");
        await Assert.That(exception.Errors[1].Span).IsEqualTo(statement.OffsetSpan);
    }

    [Test]
    [DisplayName("Resolver отклоняет inline-данные у provider-а кроме Inline")]
    public async Task Resolve_non_inline_provider_rejects_inline_data()
    {
        var resolver = new LoadProviderResolver();
        var statement = ParseLoadStatement("test: LOAD * FROM Csv(id; 1);");

        var exception = await Assert.That(async () => await resolver.ResolveAsync(statement, CreateContext()))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Message).Contains("Provider 'Csv'");
        await Assert.That(exception.Errors[0].Message).Contains("inline-данные");
        await Assert.That(exception.Errors[0].Span).IsEqualTo(statement.SourceCall.Span);
    }

    [Test]
    [DisplayName("Resolver Calendar принимает min/max режим")]
    public async Task Resolve_calendar_accepts_min_max_range()
    {
        var resolver = new LoadProviderResolver();

        var source = await resolver.ResolveAsync(
            CreateStatement(
                "Calendar",
                [
                    Option("min", "2024-01-01"),
                    Option("max", "2024-01-03")
                ]),
            CreateContext());

        await Assert.That(source.Kind).IsEqualTo("calendar");
        await Assert.That(source.RequiresBuffer).IsFalse();
    }

    [Test]
    [DisplayName("Resolver Calendar требует один режим options")]
    public async Task Resolve_calendar_rejects_missing_mode()
    {
        var resolver = new LoadProviderResolver();
        var sourceCallSpan = Span(3, 10, 20);

        var exception = await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement("Calendar", sourceCallSpan: sourceCallSpan),
                CreateContext()))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Span).IsEqualTo(sourceCallSpan);
        await Assert.That(exception.Errors[0].Message).Contains("min/max");
    }

    [Test]
    [DisplayName("Resolver Calendar проверяет формат даты")]
    public async Task Resolve_calendar_rejects_invalid_from_date()
    {
        var resolver = new LoadProviderResolver();
        var fromSpan = Span(3, 18, 36);

        var exception = await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement(
                    "Calendar",
                    [
                        Option("min", "01.01.2024", fromSpan),
                        Option("max", "2024-01-03")
                    ]),
                CreateContext()))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Span).IsEqualTo(fromSpan);
        await Assert.That(exception.Errors[0].Message).Contains("yyyy-MM-dd");
    }

    [Test]
    [DisplayName("Resolver Calendar отклоняет явный диапазон ниже безопасной границы")]
    public async Task Resolve_calendar_rejects_explicit_range_below_safe_date()
    {
        var resolver = new LoadProviderResolver();
        var minSpan = Span(3, 18, 36);

        var exception = await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement(
                    "Calendar",
                    [
                        Option("min", "1970-01-04", minSpan),
                        Option("max", "1970-01-05")
                    ]),
                CreateContext()))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Span).IsEqualTo(minSpan);
        await Assert.That(exception.Errors[0].Message).Contains("1970-01-05");
    }

    [Test]
    [DisplayName("Resolver Calendar отклоняет явный диапазон выше безопасной границы")]
    public async Task Resolve_calendar_rejects_explicit_range_above_safe_date()
    {
        var resolver = new LoadProviderResolver();
        var minSpan = Span(3, 18, 36);

        var exception = await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement(
                    "Calendar",
                    [
                        Option("min", "2148-12-31", minSpan),
                        Option("max", "2149-01-01")
                    ]),
                CreateContext()))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Span).IsEqualTo(minSpan);
        await Assert.That(exception.Errors[0].Message).Contains("2148-12-31");
    }

    [Test]
    [DisplayName("Resolver Calendar table/field требует ранее загруженную таблицу")]
    public async Task Resolve_calendar_rejects_unknown_loaded_table()
    {
        var resolver = new LoadProviderResolver();
        var tableSpan = Span(3, 18, 38);

        var exception = await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement(
                    "Calendar",
                    [
                        Option("table", "orders", tableSpan),
                        Option("field", "CreatedAt")
                    ]),
                CreateContext()))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Span).IsEqualTo(tableSpan);
        await Assert.That(exception.Errors[0].Message).Contains("orders");
    }

    [Test]
    [DisplayName("Resolver для ODBC требует SQL инструкцию")]
    public async Task Resolve_rejects_odbc_provider_without_sql()
    {
        var resolver = new LoadProviderResolver();
        var fromSpan = Span(2, 5, 9);

        var exception = await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement(
                    "Odbc",
                    [Option("connection", "Driver={PostgreSQL Unicode(x64)};Server=localhost;Database=db")],
                    fromSpan),
                CreateContext()))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Span).IsEqualTo(fromSpan);
        await Assert.That(exception.Errors[0].Message).Contains("SQL");
    }

    [Test]
    [DisplayName("Resolver Connect берет ODBC connection string и provider type из registry")]
    public async Task Resolve_connect_supports_odbc_provider_type()
    {
        var resolver = new LoadProviderResolver();
        var registry = new InMemoryConnectionRegistry(
        [
            new ScriptConnection
            {
                Name = "generic_odbc",
                Provider = ScriptConnectionType.Odbc,
                ConnectionString = "Driver={ODBC Driver 18 for SQL Server};Server=localhost;Database=db"
            }
        ]);

        var source = await resolver.ResolveAsync(
            CreateStatement(
                "Connect",
                [Option("name", "generic_odbc")],
                sql: "SELECT * FROM dbo.orders"),
            CreateContext(registry: registry));

        await Assert.That(source.Kind).IsEqualTo("odbc");
        await Assert.That(source.RequiresBuffer).IsTrue();
    }

    [Test]
    [DisplayName("Resolver Connect берет connection string и provider type из registry")]
    public async Task Resolve_connect_uses_registered_connection()
    {
        var resolver = new LoadProviderResolver();
        var registry = new InMemoryConnectionRegistry(
        [
            new ScriptConnection
            {
                Name = "main_pg",
                Provider = ScriptConnectionType.Postgres,
                ConnectionString = "Host=localhost;Database=db"
            }
        ]);

        var source = await resolver.ResolveAsync(
            CreateStatement(
                "Connect",
                [Option("name", "main_pg")],
                sql: "SELECT * FROM public.orders"),
            CreateContext(registry: registry));

        await Assert.That(source.Kind).IsEqualTo("postgres");
        await Assert.That(source.RequiresBuffer).IsFalse();
    }

    [Test]
    [DisplayName("Resolver Connect поддерживает ClickHouse provider type")]
    public async Task Resolve_connect_supports_clickhouse_provider_type()
    {
        var resolver = new LoadProviderResolver();
        var registry = new InMemoryConnectionRegistry(
        [
            new ScriptConnection
            {
                Name = "ch_dwh",
                Provider = ScriptConnectionType.ClickHouse,
                ConnectionString = "Host=localhost"
            }
        ]);

        var source = await resolver.ResolveAsync(
            CreateStatement(
                "Connect",
                [Option("name", "ch_dwh")],
                sql: "SELECT * FROM events"),
            CreateContext(registry: registry));

        await Assert.That(source.Kind).IsEqualTo("clickhouse");
        await Assert.That(source.RequiresBuffer).IsFalse();
    }

    [Test]
    [DisplayName("Resolver Connect требует name option")]
    public async Task Resolve_connect_rejects_missing_name()
    {
        var resolver = new LoadProviderResolver();
        var sourceCallSpan = Span(3, 12, 30);

        var exception = await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement(
                    "Connect",
                    sourceCallSpan: sourceCallSpan,
                    sql: "SELECT * FROM public.orders"),
                CreateContext()))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Span).IsEqualTo(sourceCallSpan);
        await Assert.That(exception.Errors[0].Message).Contains("name='connection_name'");
    }

    [Test]
    [DisplayName("Resolver Connect отклоняет name option не строкового типа")]
    public async Task Resolve_connect_rejects_non_string_name()
    {
        var resolver = new LoadProviderResolver();
        var nameSpan = Span(3, 21, 28);

        var exception = await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement(
                    "Connect",
                    [Option("name", new IntegerLiteral(1), nameSpan)],
                    sql: "SELECT * FROM public.orders"),
                CreateContext()))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Span).IsEqualTo(nameSpan);
        await Assert.That(exception.Errors[0].Message).Contains("name");
        await Assert.That(exception.Errors[0].Message).Contains("строкой");
    }

    [Test]
    [DisplayName("Resolver Connect подсказывает похожее имя connection")]
    public async Task Resolve_connect_rejects_unknown_connection_with_suggestion()
    {
        var resolver = new LoadProviderResolver();
        var nameSpan = Span(3, 21, 38);
        var registry = new InMemoryConnectionRegistry(
        [
            new ScriptConnection
            {
                Name = "main_postgres",
                Provider = ScriptConnectionType.Postgres,
                ConnectionString = "Host=localhost;Database=db"
            }
        ]);

        var exception = await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement(
                    "Connect",
                    [Option("name", "main_postgre", nameSpan)],
                    sql: "SELECT * FROM public.orders"),
                CreateContext(registry: registry)))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Span).IsEqualTo(nameSpan);
        await Assert.That(exception.Errors[0].Message).Contains("не найден");
        await Assert.That(exception.Errors[0].Message).Contains("main_postgres");
    }

    [Test]
    [DisplayName("Resolver Connect отклоняет неподдерживаемый provider type из registry")]
    public async Task Resolve_connect_rejects_unknown_connection_provider_type()
    {
        var resolver = new LoadProviderResolver();
        var nameSpan = Span(3, 21, 30);
        var registry = new InMemoryConnectionRegistry(
        [
            new ScriptConnection
            {
                Name = "main_pg",
                Provider = (ScriptConnectionType)999,
                ConnectionString = "Host=localhost;Database=db"
            }
        ]);

        var exception = await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement(
                    "Connect",
                    [Option("name", "main_pg", nameSpan)],
                    sql: "SELECT * FROM public.orders"),
                CreateContext(registry: registry)))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Span).IsEqualTo(nameSpan);
        await Assert.That(exception.Errors[0].Message).Contains("неподдерживаемый provider");
        await Assert.That(exception.Errors[0].Message).Contains("999");
    }

    [Test]
    [DisplayName("Resolver Connect отклоняет лишнюю connection option")]
    public async Task Resolve_connect_rejects_direct_connection_option()
    {
        var resolver = new LoadProviderResolver();
        var connectionSpan = Span(3, 21, 55);

        var exception = await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement(
                    "Connect",
                    [
                        Option("name", "main_pg"),
                        Option("connection", "Host=localhost", connectionSpan)
                    ],
                    sql: "SELECT * FROM public.orders"),
                CreateContext()))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors.Select(static error => error.Span).ToArray())
            .Contains(connectionSpan);
        await Assert.That(exception.Errors.Any(static error => error.Message.Contains("не поддерживается")))
            .IsTrue();
    }

    [Test]
    [DisplayName("Resolver Connect требует SQL инструкцию")]
    public async Task Resolve_connect_rejects_missing_sql()
    {
        var resolver = new LoadProviderResolver();
        var fromSpan = Span(3, 1, 5);
        var registry = new InMemoryConnectionRegistry(
        [
            new ScriptConnection
            {
                Name = "main_pg",
                Provider = ScriptConnectionType.Postgres,
                ConnectionString = "Host=localhost;Database=db"
            }
        ]);

        var exception = await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement(
                    "Connect",
                    [Option("name", "main_pg")],
                    fromSpan),
                CreateContext(registry: registry)))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Span).IsEqualTo(fromSpan);
        await Assert.That(exception.Errors[0].Message).Contains("требуется SQL после FROM");
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
    [DisplayName("Resolver для DB provider отклоняет table option")]
    public async Task Resolve_rejects_database_provider_table_option()
    {
        var resolver = new LoadProviderResolver();
        var tableSpan = Span(5, 30, 52);

        var exception = await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement(
                    "Hive",
                    [
                        Option("connection", "Driver={Hive};Host=localhost;Port=10000;Schema=default"),
                        Option("table", "default.orders", tableSpan)
                    ],
                    sql: "SELECT * FROM default.orders"),
                CreateContext()))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Span).IsEqualTo(tableSpan);
        await Assert.That(exception.Errors[0].Message).Contains("не поддерживается");
    }

    [Test]
    [DisplayName("Hive provider ошибку ODBC соединения оборачивает в DbExecutionException")]
    [TestWithDependency(DatabaseDependency.ApacheHive, UseDataSource = false, CheckExternalDependencies = false)]
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

    [Test]
    [DisplayName("Resolver Table provider ищет ранее загруженную таблицу по alias")]
    public async Task Resolve_table_provider_rejects_unknown_loaded_table_alias()
    {
        var resolver = new LoadProviderResolver();
        var sourceSpan = Span(3, 20, 30);

        var exception = await Assert.That(async () => await resolver.ResolveAsync(
                CreateStatement("Table", [Option("name", "raw_orders", sourceSpan)]),
                CreateContext()))
            .ThrowsExactly<ProviderResolutionException>();

        await Assert.That(exception!.Errors).Count().IsEqualTo(1);
        await Assert.That(exception.Errors[0].Span).IsEqualTo(sourceSpan);
        await Assert.That(exception.Errors[0].Message).Contains("raw_orders");
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
            TableName = "test",
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

    private static LoadStatement ParseLoadStatement(string text)
    {
        return (LoadStatement)Statement.Parse(text).Value!;
    }

    private static LoadOption Option(string name, string value)
    {
        return Option(name, value, Span());
    }

    private static LoadOption Option(string name, long value)
    {
        return Option(name, value, Span());
    }

    private static LoadOption Option(string name, long value, LangSpan span)
    {
        return Option(name, new IntegerLiteral(value), span);
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

    private static async Task<long[]> ReadNumbersAsync(DbDataReader reader)
    {
        var values = new List<long>();
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            values.Add(reader.GetInt64(0));
        }

        return values.ToArray();
    }

    private static ScriptContext CreateContext(
        IFileSource? fileSource = null,
        IConnectionRegistry? registry = null)
    {
        return new ScriptContext
        {
            FileStorage = fileSource ?? new StubFileSource(),
            TargetConnectionString = "Host=clickhouse",
            Logger = NullLogger.Instance,
            ConnectionRegistry = registry ?? EmptyConnectionRegistry.Instance
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

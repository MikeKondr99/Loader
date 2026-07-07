using System.Data.Common;
using Loader.Core.Data;
using Loader.Core.Providers;
using Loader.Core.Providers.Postgres;
using Loader.Core.Providers.Sql;
using Loader.Core.Sources;
using Loader.Core.Tests.Infrastructure;

namespace Loader.Core.Tests;

public sealed class PostgresProviderTests
{
    private static readonly PostgresProvider Provider = new();
    private static PostgresTestDatabase? Database;

    [Before(Class)]
    public static async Task StartDatabase()
    {
        Database = await PostgresTestDatabase.StartAsync();
    }

    [After(Class)]
    public static async Task StopDatabase()
    {
        if (Database is not null)
        {
            await Database.DisposeAsync();
        }
    }

    [Test]
    [MethodDataSource(nameof(SqlValueCases))]
    [DisplayName("Postgres sql-выражение выдает ожидаемый canonical value")]
    public async Task Sql_expression_maps_to_expected_value(string sqlExpression, DataType expectedType, object expected)
    {
        await using var rawReader = await OpenReaderAsync($"select {sqlExpression} as value");
        await using var reader = rawReader.AsTyped();

        await Assert.That(reader).HaveData(
            columns: ["value"],
            types: [expectedType],
            rows: [
                ValueTuple.Create(expected)
            ]);
    }

    [Test]
    [DisplayName("Postgres пустой результат сохраняет имена и типы схемы")]
    public async Task Empty_result_preserves_schema()
    {
        await using var rawReader = await OpenReaderAsync(
            """
            select
                1::smallint as id,
                1.3::decimal(10,3) as dec,
                'Moscow'::varchar(30) as name,
                true::boolean as active
            where false
            """);
        await using var reader = rawReader.AsTyped();

        await Assert.That(reader).HaveData(
            columns: ["id","dec", "name", "active"],
            types: [DataType.Integer, DataType.Number, DataType.Text, DataType.Boolean],
            rows: []);
    }

    [Test]
    [DisplayName("Postgres aliases возвращают lowercase без кавычек и точное имя с кавычками")]
    public async Task Aliases_return_lowercase_without_quotes_and_exact_name_with_quotes()
    {
        await using var rawReader = await OpenReaderAsync(
            """
            select
                1::integer as IdValue,
                'Moscow'::text as "CityName"
            """);
        await using var reader = rawReader.AsTyped();

        await Assert.That(reader).HaveData(
            columns: ["idvalue", "CityName"],
            types: [DataType.Integer, DataType.Text],
            rows: [
                (1L, "Moscow")
            ]);
        await Assert.That(() => reader.GetOrdinal("IdValue"))
            .ThrowsExactly<IndexOutOfRangeException>()
            .WithMessage("Column 'IdValue' was not found.");
        await Assert.That(() => reader.GetOrdinal("cityname"))
            .ThrowsExactly<IndexOutOfRangeException>()
            .WithMessage("Column 'cityname' was not found.");
    }

    [Test]
    [DisplayName("Postgres несколько строк читаются потоково в порядке результата")]
    public async Task Reads_multiple_rows_in_result_order()
    {
        await using var rawReader = await OpenReaderAsync(
            """
            select *
            from (
                values
                    (1::integer, 'first'::text),
                    (2::integer, 'second'::text),
                    (3::integer, 'third'::text)
            ) as rows(id, name)
            order by id
            """);
        await using var reader = rawReader.AsTyped();

        await Assert.That(reader).HaveData(
            columns: ["id", "name"],
            types: [DataType.Integer, DataType.Text],
            rows: [
                (1L, "first"),
                (2L, "second"),
                (3L, "third")
            ]);
    }

    [Test]
    [DisplayName("Postgres provider работает вместе с Where поверх Domain reader")]
    public async Task Supports_where_over_typed_postgres_reader()
    {
        await using var rawReader = await OpenReaderAsync(
            """
            select *
            from (
                values
                    (1::integer, 'Moscow'::text),
                    (2::integer, 'London'::text),
                    (3::integer, 'Moscow'::text)
            ) as rows(id, city)
            order by id
            """);
        await using var reader = rawReader
            .AsTyped()
            .Where(row => row.Text("city") == "Moscow" && row.Integer("id") > 1);

        await Assert.That(reader).HaveData(
            columns: ["id", "city"],
            types: [DataType.Integer, DataType.Text],
            rows: [
                (3L, "Moscow")
            ]);
    }

    [Test]
    [DisplayName("Postgres GetDataTypeName оставляет origin type name")]
    public async Task Keeps_origin_data_type_name_available()
    {
        await using var rawReader = await OpenReaderAsync(
            """
            select
                '{"city":"Moscow"}'::jsonb as jsonb_value,
                1::integer as integer_value
            """);
        await using var reader = rawReader.AsTyped();

        await Assert.That(reader.GetDataTypeName(0)).IsEqualTo("jsonb");
        await Assert.That(reader.GetDataTypeName(1)).IsEqualTo("integer");
    }

    [Test]
    [DisplayName("Postgres null значение выдает DBNull и сохраняет тип схемы")]
    public async Task Null_value_returns_dbnull()
    {
        await using var rawReader = await OpenReaderAsync("select null::integer as value");
        await using var reader = rawReader.AsTyped();

        await Assert.That(reader).HaveData(
            columns: ["value"],
            types: [DataType.Integer],
            rows: [
                ValueTuple.Create(DBNull.Value)
            ]);
    }

    [Test]
    [DisplayName("Postgres SELECT 1 без alias выдает имя колонки от Postgres")]
    public async Task Select_without_alias_uses_postgres_generated_column_name()
    {
        await using var rawReader = await OpenReaderAsync("select 1");
        await using var reader = rawReader.AsTyped();

        await Assert.That(reader).HaveData(
            columns: ["?column?"],
            types: [DataType.Integer],
            rows: [
                ValueTuple.Create(1L)
            ]);
    }

    [Test]
    [DisplayName("Postgres ошибка SQL запроса оборачивается в DbExecutionException")]
    public async Task Query_error_is_wrapped_in_provider_exception()
    {
        await Assert.That(async () => await OpenReaderAsync("select * from table_that_does_not_exist"))
            .ThrowsExactly<DbExecutionException>()
            .WithMessage("Database query failed for provider 'postgres': select * from table_that_does_not_exist");
    }

    [Test]
    [DisplayName("Postgres слишком большой numeric при чтении оборачивается в DataReaderValueException")]
    public async Task Oversized_numeric_value_error_is_wrapped_in_reader_value_exception()
    {
        await using var rawReader = await OpenReaderAsync("select repeat('9', 100)::numeric as value");
        await using var reader = rawReader.AsTyped();

        await Assert.That(() => reader.Read())
            .ThrowsExactly<DataReaderValueException>()
            .WithMessage("Failed to read field 'value' at ordinal 0.");
    }

    [Test]
    [DisplayName("Postgres повторяющиеся имена колонок кидают явную ошибку схемы")]
    public async Task Duplicate_column_names_throw_schema_exception()
    {
        await using var rawReader = await OpenReaderAsync("select 1 as value, 2 as value");

        await Assert.That(() => rawReader.AsTyped())
            .ThrowsExactly<DuplicateDataFieldNameException>()
            .WithMessage("Column name 'value' is duplicated.");
    }

    [Test]
    [DisplayName("Postgres CollectMeta берет decimal precision и scale из column schema")]
    public async Task Collect_meta_reads_decimal_precision_and_scale_from_column_schema()
    {
        var meta = new DataMetaContainer();
        await using var rawReader = await OpenReaderAsync("select 12.34::numeric(10, 2) as amount");
        await using var reader = rawReader
            .AsTyped()
            .CollectMeta(meta);

        await Assert.That(reader).HaveData(
            columns: ["amount"],
            types: [DataType.Number],
            rows: [
                ValueTuple.Create(12.34m)
            ]);

        await Assert.That(meta.Success).IsTrue();
        await Assert.That(meta.Columns[0].DecimalPrecision).IsEqualTo(10);
        await Assert.That(meta.Columns[0].DecimalScale).IsEqualTo(2);
    }

    public static IEnumerable<(string SqlExpression, DataType ExpectedType, object Expected)> SqlValueCases()
    {
        yield return ("'example'::text", DataType.Text, "example");
        yield return ("'example'::varchar(32)", DataType.Text, "example");
        yield return ("'x'::char(1)", DataType.Text, "x");
        yield return ("'abc'::character(3)", DataType.Text, "abc");
        yield return ("'example'::name", DataType.Text, "example");
        yield return ("'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid", DataType.Text, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        yield return ("'{}'::json", DataType.Text, "{}");
        yield return ("'{}'::jsonb", DataType.Text, "{}");
        yield return ("'<root />'::xml", DataType.Text, "<root />");
        yield return ("E'\\\\xDEADBEEF'::bytea", DataType.Text, "\\xdeadbeef");
        yield return ("B'1'::bit(1)", DataType.Boolean, true);
        yield return ("B'1010'::bit(4)", DataType.Text, "1010");
        yield return ("B'101'::bit varying(8)", DataType.Text, "101");
        yield return ("1::smallint", DataType.Integer, 1L);
        yield return ("2::integer", DataType.Integer, 2L);
        yield return ("3::bigint", DataType.Integer, 3L);
        yield return ("4::oid", DataType.Integer, 4L);
        yield return ("'5'::xid", DataType.Integer, 5L);
        yield return ("'6'::cid", DataType.Integer, 6L);
        yield return ("'1 2 3'::oidvector", DataType.Text, "{1,2,3}");
        yield return ("1.5::real", DataType.Number, 1.5m);
        yield return ("2.25::double precision", DataType.Number, 2.25m);
        yield return ("12.34::money", DataType.Number, 12.34m);
        yield return ("12345.6789::numeric", DataType.Number, 12345.6789m);
        yield return ("9::numeric(1)", DataType.Number, 9m);
        yield return ("1234567890::numeric(10, 0)", DataType.Number, 1234567890m);
        yield return ("0.1234567890::numeric(10, 10)", DataType.Number, 0.1234567890m);
        yield return ("12345.6789::numeric(12, 4)", DataType.Number, 12345.6789m);
        yield return ("timestamp '2026-01-02 03:04:05'", DataType.DateTime, new DateTime(2026, 1, 2, 3, 4, 5));
        yield return ("timestamp with time zone '2026-01-02 03:04:05+00'", DataType.DateTime, new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc));
        yield return ("date '2026-01-02'", DataType.Date, new DateOnly(2026, 1, 2));
        yield return ("time '03:04:05'", DataType.Time, new TimeOnly(3, 4, 5));
        yield return ("time with time zone '03:04:05+00'", DataType.Text, "0001-01-02T03:04:05.0000000+00:00");
        yield return ("interval '03:04:05'", DataType.Time, new TimeOnly(3, 4, 5));
        yield return ("true::boolean", DataType.Boolean, true);
        yield return ("'192.168.1.1'::inet", DataType.Text, "192.168.1.1");
        yield return ("'192.168.0.0/24'::cidr", DataType.Text, "192.168.0.0/24");
        yield return ("'08:00:2b:01:02:03'::macaddr", DataType.Text, "08:00:2b:01:02:03");
        yield return ("'08:00:2b:01:02:03:04:05'::macaddr8", DataType.Text, "08:00:2b:01:02:03:04:05");
        yield return ("point '(1,2)'", DataType.Text, "(1,2)");
        yield return ("lseg '[(0,0),(1,1)]'", DataType.Text, "[(0,0),(1,1)]");
        yield return ("box '((0,0),(1,1))'", DataType.Text, "(1,1),(0,0)");
        yield return ("path '((0,0),(1,1),(2,0))'", DataType.Text, "((0,0),(1,1),(2,0))");
        yield return ("polygon '((0,0),(1,0),(1,1),(0,1))'", DataType.Text, "((0,0),(1,0),(1,1),(0,1))");
        yield return ("circle '<(0,0),1>'", DataType.Text, "<(0,0),1>");
        yield return ("to_tsquery('english', 'hello & world')", DataType.Text, "'hello' & 'world'");
        yield return ("to_tsvector('english', 'hello world')", DataType.Text, "'hello':1 'world':2");
        yield return ("'16/B374D848'::pg_lsn", DataType.Text, "16/B374D848");
        yield return ("int4range(1, 3)", DataType.Text, "[1,3)");
        yield return ("int8range(1, 3)", DataType.Text, "[1,3)");
        yield return ("numrange(1.5, 3.5)", DataType.Text, "[1.5,3.5)");
        yield return ("tsrange(timestamp '2026-01-02', timestamp '2026-01-03')", DataType.Text, "[2026-01-02 00:00:00,2026-01-03 00:00:00)");
        yield return ("daterange(date '2026-01-02', date '2026-01-03')", DataType.Text, "[2026-01-02,2026-01-03)");
        yield return ("array[1, 2, 3]::integer[]", DataType.Text, "{1,2,3}");
        yield return ("array['a', 'b']::text[]", DataType.Text, "{a,b}");
    }

    private static ValueTask<DbDataReader> OpenReaderAsync(string sql)
    {
        var database = Database ?? throw new InvalidOperationException("Postgres test database is not started.");
        return Provider.OpenReaderAsync(
            new ConnectionStringSource
            {
                ConnectionString = database.ConnectionString
            },
            new SqlTableConfig
            {
                Sql = sql
            });
    }
}

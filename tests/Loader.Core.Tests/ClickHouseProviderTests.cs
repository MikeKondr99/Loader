using System.Data.Common;
using Loader.Core.Data;
using Loader.Core.Providers;
using Loader.Core.Providers.ClickHouse;
using Loader.Core.Providers.Sql;
using Loader.Core.Sources;
using Loader.Core.Tests.Infrastructure;

namespace Loader.Core.Tests;

public sealed class ClickHouseProviderTests
{
    private static readonly ClickHouseProvider Provider = new();
    private static ClickHouseTestDatabase? Database;

    [Before(Class)]
    public static async Task StartDatabase()
    {
        Database = await ClickHouseTestDatabase.StartAsync();
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
    [DisplayName("ClickHouse sql-выражение выдает ожидаемый canonical value")]
    public async Task Sql_expression_maps_to_expected_value(string sqlExpression, DataType expectedType, object expected)
    {
        await using var rawReader = await OpenReaderAsync($"select {sqlExpression} as value");
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["value"],
            types: [expectedType],
            rows: [
                ValueTuple.Create(expected)
            ]);
    }

    [Test]
    [DisplayName("ClickHouse пустой результат сохраняет имена и типы схемы")]
    public async Task Empty_result_preserves_schema()
    {
        await using var rawReader = await OpenReaderAsync(
            """
            select
                toInt32(1) as id,
                toDecimal32(12.34, 2) as amount,
                toString('Moscow') as city,
                true as active
            where false
            """);
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "amount", "city", "active"],
            types: [DataType.Integer, DataType.Number, DataType.Text, DataType.Boolean],
            rows: []);
    }

    [Test]
    [DisplayName("ClickHouse aliases сохраняют имя результата запроса")]
    public async Task Aliases_return_result_column_names()
    {
        await using var rawReader = await OpenReaderAsync(
            """
            select
                toInt32(1) as IdValue,
                toString('Moscow') as CityName
            """);
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["IdValue", "CityName"],
            types: [DataType.Integer, DataType.Text],
            rows: [
                (1, "Moscow")
            ]);
        await Assert.That(() => reader.GetOrdinal("idvalue"))
            .ThrowsExactly<IndexOutOfRangeException>()
            .WithMessage("Column 'idvalue' was not found.");
    }

    [Test]
    [DisplayName("ClickHouse несколько строк читаются потоково в порядке результата")]
    public async Task Reads_multiple_rows_in_result_order()
    {
        await using var rawReader = await OpenReaderAsync(
            """
            select *
            from values('id Int32, name String', (1, 'first'), (2, 'second'), (3, 'third'))
            order by id
            """);
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "name"],
            types: [DataType.Integer, DataType.Text],
            rows: [
                (1, "first"),
                (2, "second"),
                (3, "third")
            ]);
    }

    [Test]
    [DisplayName("ClickHouse provider работает вместе с Where поверх Domain reader")]
    public async Task Supports_where_over_domain_clickhouse_reader()
    {
        await using var rawReader = await OpenReaderAsync(
            """
            select *
            from values('id Int32, city String', (1, 'Moscow'), (2, 'London'), (3, 'Moscow'))
            order by id
            """);
        await using var reader = rawReader
            .Normalize()
            .Where(row => row.Text("city") == "Moscow" && row.Integer("id") > 1);

        await Assert.That(reader).HaveData(
            columns: ["id", "city"],
            types: [DataType.Integer, DataType.Text],
            rows: [
                (3, "Moscow")
            ]);
    }

    [Test]
    [DisplayName("ClickHouse GetDataTypeName оставляет origin type name")]
    public async Task Keeps_origin_data_type_name_available()
    {
        await using var rawReader = await OpenReaderAsync(
            """
            select
                toDecimal64(12.34, 2) as amount,
                toDateTime('2026-01-02 03:04:05') as created
            """);
        await using var reader = rawReader.Normalize();

        await Assert.That(reader.GetDataTypeName(0)).IsEqualTo("Decimal64(2)");
        await Assert.That(reader.GetDataTypeName(1)).IsEqualTo("DateTime(UTC)");
    }

    [Test]
    [DisplayName("ClickHouse null значение выдает DBNull и сохраняет тип схемы")]
    public async Task Null_value_returns_dbnull()
    {
        await using var rawReader = await OpenReaderAsync("select cast(null, 'Nullable(Int32)') as value");
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["value"],
            types: [DataType.Integer],
            rows: [
                ValueTuple.Create(DBNull.Value)
            ]);
    }

    [Test]
    [DisplayName("ClickHouse SELECT 1 без alias выдает имя колонки от ClickHouse")]
    public async Task Select_without_alias_uses_clickhouse_generated_column_name()
    {
        await using var rawReader = await OpenReaderAsync("select 1");
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["1"],
            types: [DataType.Integer],
            rows: [
                ValueTuple.Create((byte)1)
            ]);
    }

    [Test]
    [DisplayName("ClickHouse ошибка SQL запроса оборачивается в DbExecutionException")]
    public async Task Query_error_is_wrapped_in_provider_exception()
    {
        await Assert.That(async () => await OpenReaderAsync("select * from table_that_does_not_exist"))
            .ThrowsExactly<DbExecutionException>()
            .WithMessage("Database query failed for provider 'clickhouse': select * from table_that_does_not_exist");
    }

    [Test]
    [DisplayName("ClickHouse повторяющиеся alias отклоняются на уровне выполнения запроса")]
    public async Task Duplicate_column_names_throw_query_execution_exception()
    {
        await Assert.That(async () => await OpenReaderAsync("select 1 as value, 2 as value"))
            .ThrowsExactly<DbExecutionException>()
            .WithMessage("Database query failed for provider 'clickhouse': select 1 as value, 2 as value");
    }

    [Test]
    [DisplayName("ClickHouse CollectMeta берет decimal precision и scale из column schema")]
    public async Task Collect_meta_reads_decimal_precision_and_scale_from_column_schema()
    {
        var meta = new DataMetaContainer();
        await using var rawReader = await OpenReaderAsync("select toDecimal64(12.34, 2) as amount");
        await using var reader = rawReader
            .Normalize()
            .CollectMeta(meta);

        await Assert.That(reader).HaveData(
            columns: ["amount"],
            types: [DataType.Number],
            rows: [
                ValueTuple.Create(12.34m)
            ]);

        await Assert.That(meta.Success).IsTrue();
        await Assert.That(meta.Columns[0].DecimalPrecision).IsEqualTo(18);
        await Assert.That(meta.Columns[0].DecimalScale).IsEqualTo(2);
    }

    public static IEnumerable<(string SqlExpression, DataType ExpectedType, object Expected)> SqlValueCases()
    {
        yield return ("toString('example')", DataType.Text, "example");
        yield return ("toUUID('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa')", DataType.Text, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        yield return ("toInt8(-1)", DataType.Integer, (sbyte)-1);
        yield return ("toUInt8(1)", DataType.Integer, (byte)1);
        yield return ("toInt16(-2)", DataType.Integer, (short)-2);
        yield return ("toUInt16(2)", DataType.Integer, (ushort)2);
        yield return ("toInt32(-3)", DataType.Integer, -3);
        yield return ("toUInt32(3)", DataType.Integer, 3u);
        yield return ("toInt64(-4)", DataType.Integer, -4L);
        yield return ("toUInt64(4)", DataType.Integer, 4UL);
        yield return ("toFloat32(1.5)", DataType.Number, 1.5f);
        yield return ("toFloat64(2.25)", DataType.Number, 2.25d);
        yield return ("toDecimal32(12.34, 2)", DataType.Number, 12.34m);
        yield return ("toDecimal64(12345.6789, 4)", DataType.Number, 12345.6789m);
        yield return ("toDecimal128(12345.6789, 4)", DataType.Number, 12345.6789m);
        yield return ("toDateTime('2026-01-02 03:04:05')", DataType.DateTime, new DateTime(2026, 1, 2, 3, 4, 5));
        yield return ("toDate('2026-01-02')", DataType.DateTime, new DateTime(2026, 1, 2));
        yield return ("true", DataType.Boolean, true);
        yield return ("toIPv4('192.168.1.1')", DataType.Text, "192.168.1.1");
        yield return ("toIPv6('2001:db8::1')", DataType.Text, "2001:db8::1");
        yield return ("['a', 'b']", DataType.Text, "{a,b}");
    }

    private static ValueTask<DbDataReader> OpenReaderAsync(string sql)
    {
        var database = Database ?? throw new InvalidOperationException("ClickHouse test database is not started.");
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

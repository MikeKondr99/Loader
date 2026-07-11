using System.Data.Common;
using Loader.Core.Providers;
using Loader.Core.Providers.Sql;
using Loader.Core.Providers.SqlServer;
using Loader.Core.Sources;
using Loader.Core.Tests.Infrastructure;

namespace Loader.Core.Tests;

public sealed class SqlServerProviderTests
{
    private static readonly SqlServerProvider Provider = new();
    private static SqlServerTestDatabase? Database;

    [Before(Class)]
    public static async Task StartDatabase()
    {
        Database = await SqlServerTestDatabase.StartAsync();
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
    [DisplayName("SqlServer sql-выражение выдает ожидаемый canonical value")]
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
    [DisplayName("SqlServer пустой результат сохраняет имена и типы схемы")]
    public async Task Empty_result_preserves_schema()
    {
        await using var rawReader = await OpenReaderAsync(
            """
            select
                cast(1 as int) as id,
                cast(12.34 as decimal(10, 2)) as amount,
                cast('Moscow' as nvarchar(30)) as city,
                cast(1 as bit) as active
            where 1 = 0
            """);
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "amount", "city", "active"],
            types: [DataType.Integer, DataType.Number, DataType.Text, DataType.Boolean],
            rows: []);
    }

    [Test]
    [DisplayName("SqlServer aliases сохраняют имя результата запроса")]
    public async Task Aliases_return_result_column_names()
    {
        await using var rawReader = await OpenReaderAsync(
            """
            select
                cast(1 as int) as IdValue,
                cast('Moscow' as nvarchar(30)) as CityName
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
    [DisplayName("SqlServer несколько строк читаются потоково в порядке результата")]
    public async Task Reads_multiple_rows_in_result_order()
    {
        await using var rawReader = await OpenReaderAsync(
            """
            select *
            from (values
                (cast(1 as int), cast('first' as nvarchar(30))),
                (cast(2 as int), cast('second' as nvarchar(30))),
                (cast(3 as int), cast('third' as nvarchar(30)))
            ) as rows(id, name)
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
    [DisplayName("SqlServer provider работает вместе с Where поверх Domain reader")]
    public async Task Supports_where_over_domain_sqlserver_reader()
    {
        await using var rawReader = await OpenReaderAsync(
            """
            select *
            from (values
                (cast(1 as int), cast('Moscow' as nvarchar(30))),
                (cast(2 as int), cast('London' as nvarchar(30))),
                (cast(3 as int), cast('Moscow' as nvarchar(30)))
            ) as rows(id, city)
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
    [DisplayName("SqlServer GetDataTypeName оставляет origin type name")]
    public async Task Keeps_origin_data_type_name_available()
    {
        await using var rawReader = await OpenReaderAsync(
            """
            select
                cast(12.34 as decimal(10, 2)) as amount,
                cast('2026-01-02T03:04:05' as datetime2) as created
            """);
        await using var reader = rawReader.Normalize();

        await Assert.That(reader.GetDataTypeName(0)).IsEqualTo("decimal");
        await Assert.That(reader.GetDataTypeName(1)).IsEqualTo("datetime2");
    }

    [Test]
    [DisplayName("SqlServer null значение выдает DBNull и сохраняет тип схемы")]
    public async Task Null_value_returns_dbnull()
    {
        await using var rawReader = await OpenReaderAsync("select cast(null as int) as value");
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["value"],
            types: [DataType.Integer],
            rows: [
                ValueTuple.Create(DBNull.Value)
            ]);
    }

    [Test]
    [DisplayName("SqlServer Nullable базовых типов сохраняет базовый DataType и читает DBNull")]
    public async Task Nullable_base_types_preserve_schema_type_and_read_dbnull()
    {
        await using var rawReader = await OpenReaderAsync(
            """
            select
                cast(null as nvarchar(30)) as text_value,
                cast(null as decimal(10, 2)) as amount_value,
                cast(null as datetime2) as created_value,
                cast(null as bit) as active_value
            """);
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["text_value", "amount_value", "created_value", "active_value"],
            types: [DataType.Text, DataType.Number, DataType.DateTime, DataType.Boolean],
            rows: [
                (DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value)
            ]);
    }

    [Test]
    [DisplayName("SqlServer SELECT 1 без alias выдает имя колонки от SqlServer")]
    public async Task Select_without_alias_uses_sqlserver_generated_column_name()
    {
        await using var rawReader = await OpenReaderAsync("select 1");
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: [""],
            types: [DataType.Integer],
            rows: [
                ValueTuple.Create(1)
            ]);
    }

    [Test]
    [DisplayName("SqlServer ошибка SQL запроса оборачивается в DbExecutionException")]
    public async Task Query_error_is_wrapped_in_provider_exception()
    {
        await Assert.That(async () => await OpenReaderAsync("select * from table_that_does_not_exist"))
            .ThrowsExactly<DbExecutionException>()
            .WithMessage("Database query failed for provider 'sqlserver': select * from table_that_does_not_exist");
    }

    [Test]
    [DisplayName("SqlServer повторяющиеся имена колонок кидают явную ошибку схемы")]
    public async Task Duplicate_column_names_throw_schema_exception()
    {
        await using var rawReader = await OpenReaderAsync("select 1 as value, 2 as value");

        await Assert.That(() => rawReader.Normalize())
            .ThrowsExactly<DuplicateDataFieldNameException>()
            .WithMessage("Column name 'value' is duplicated.");
    }

    [Test]
    [MethodDataSource(nameof(UnsupportedSqlValueCases))]
    [DisplayName("SqlServer явно неподдержанный тип выдает DBNull без чтения значения")]
    public async Task Unsupported_sql_expression_maps_to_dbnull(string sqlExpression, DataType expectedType)
    {
        await using var rawReader = await OpenReaderAsync($"select {sqlExpression} as value");
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["value"],
            types: [DataType.Text],
            rows: [
                ValueTuple.Create(DBNull.Value)
            ]);
    }

    [Test]
    [DisplayName("SqlServer слишком большой decimal при чтении оборачивается в DataReaderValueException")]
    public async Task Oversized_decimal_value_error_is_wrapped_in_reader_value_exception()
    {
        await using var rawReader = await OpenReaderAsync("select cast(99999999999999999999999999999999999999 as decimal(38, 0)) as value");
        await using var reader = rawReader.Normalize();

        await Assert.That(() => reader.Read())
            .ThrowsExactly<DataReaderValueException>()
            .WithMessage("Failed to read field 'value' at ordinal 0.");
    }

    [Test]
    [DisplayName("SqlServer sql_variant остается unknown потому что драйвер отдает CLR object")]
    public async Task Sql_variant_is_unknown_object_type()
    {
        await using var rawReader = await OpenReaderAsync("select cast(cast(42 as int) as sql_variant) as value");

        await Assert.That(() => rawReader.Normalize())
            .ThrowsExactly<UnknownClrTypeException>()
            .WithMessage("CLR type 'System.Object' is unknown to Loader data type mapper.");
    }

    [Test]
    [DisplayName("SqlServer CollectMeta не выдумывает decimal precision и scale если драйвер их не отдал")]
    public async Task Collect_meta_keeps_decimal_precision_and_scale_empty_when_driver_does_not_provide_them()
    {
        var meta = new DataMetaContainer();
        await using var rawReader = await OpenReaderAsync("select cast(12.34 as decimal(10, 2)) as amount");
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
        await Assert.That(meta.Columns[0].DecimalPrecision).IsNull();
        await Assert.That(meta.Columns[0].DecimalScale).IsNull();
    }

    public static IEnumerable<(string SqlExpression, DataType ExpectedType, object Expected)> SqlValueCases()
    {
        yield return ("cast('example' as varchar(32))", DataType.Text, "example");
        yield return ("cast('example' as varchar(max))", DataType.Text, "example");
        yield return ("cast(N'пример' as nvarchar(32))", DataType.Text, "пример");
        yield return ("cast(N'пример' as nvarchar(max))", DataType.Text, "пример");
        yield return ("cast('x' as char(1))", DataType.Text, "x");
        yield return ("cast('abc' as nchar(3))", DataType.Text, "abc");
        yield return ("cast('legacy text' as text)", DataType.Text, "legacy text");
        yield return ("cast(N'legacy ntext' as ntext)", DataType.Text, "legacy ntext");
        yield return ("cast('<root />' as xml)", DataType.Text, "<root />");
        yield return ("cast('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa' as uniqueidentifier)", DataType.Text, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        yield return ("cast(255 as tinyint)", DataType.Integer, (byte)255);
        yield return ("cast(-2 as smallint)", DataType.Integer, (short)-2);
        yield return ("cast(-3 as int)", DataType.Integer, -3);
        yield return ("cast(-4 as bigint)", DataType.Integer, -4L);
        yield return ("cast(12.34 as smallmoney)", DataType.Number, 12.34m);
        yield return ("cast(12.34 as money)", DataType.Number, 12.34m);
        yield return ("cast(9 as decimal(1, 0))", DataType.Number, 9m);
        yield return ("cast(0.1234567890 as decimal(10, 10))", DataType.Number, 0.1234567890m);
        yield return ("cast(12345.6789 as decimal(12, 4))", DataType.Number, 12345.6789m);
        yield return ("cast(12345.6789 as numeric(12, 4))", DataType.Number, 12345.6789m);
        yield return ("cast(1.5 as real)", DataType.Number, 1.5f);
        yield return ("cast(2.25 as float)", DataType.Number, 2.25d);
        yield return ("cast('2026-01-02T03:04:05' as datetime)", DataType.DateTime, new DateTime(2026, 1, 2, 3, 4, 5));
        yield return ("cast('2026-01-02T03:04:05' as datetime2)", DataType.DateTime, new DateTime(2026, 1, 2, 3, 4, 5));
        yield return ("cast('2026-01-02T03:04:00' as smalldatetime)", DataType.DateTime, new DateTime(2026, 1, 2, 3, 4, 0));
        yield return ("cast('2026-01-02' as date)", DataType.DateTime, new DateTime(2026, 1, 2));
        yield return ("cast('03:04:05' as time)", DataType.Time, new TimeOnly(3, 4, 5));
        yield return ("cast('2026-01-02T03:04:05+00:00' as datetimeoffset)", DataType.Text, "2026-01-02T03:04:05.0000000+00:00");
        yield return ("cast(1 as bit)", DataType.Boolean, true);
    }

    public static IEnumerable<(string SqlExpression, DataType ExpectedType)> UnsupportedSqlValueCases()
    {
        yield return ("cast(0xDEADBEEF as binary(4))", DataType.Text);
        yield return ("cast(0xDEADBEEF as varbinary(max))", DataType.Text);
        yield return ("cast(0xDEADBEEF as image)", DataType.Text);
        yield return ("cast(0x0000000000000001 as rowversion)", DataType.Text);
    }

    private static ValueTask<DbDataReader> OpenReaderAsync(string sql)
    {
        var database = Database ?? throw new InvalidOperationException("SqlServer test database is not started.");
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

using System.Data.Common;
using Loader.Core.Data;
using Loader.Core.Providers;
using Loader.Core.Providers.Oracle;
using Loader.Core.Providers.Sql;
using Loader.Core.Sources;
using Loader.Core.Tests.Infrastructure;

namespace Loader.Core.Tests;

public sealed class OracleProviderTests
{
    private const string ConnectionStringEnvironmentVariable = "ORACLE_TEST_CONNECTION_STRING";
    private static readonly OracleProvider Provider = new();

    [Test]
    [DisplayName("Oracle provider возвращает ожидаемый provider kind")]
    public async Task Kind_is_oracle()
    {
        await Assert.That(Provider.Kind).IsEqualTo("oracle");
    }

    [Test]
    [MethodDataSource(nameof(SqlValueCases))]
    [DisplayName("Oracle sql-выражение мапится в ожидаемое canonical value")]
    public async Task Sql_expression_maps_to_expected_value(string sqlExpression, DataType expectedType, object expected)
    {
        RequireOracleConnectionString();

        await using var rawReader = await OpenReaderAsync($"select {sqlExpression} as \"value\" from dual");
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["value"],
            types: [expectedType],
            rows: [
                ValueTuple.Create(expected)
            ]);
    }

    [Test]
    [DisplayName("Oracle пустой результат сохраняет имена и типы схемы")]
    public async Task Empty_result_preserves_schema()
    {
        RequireOracleConnectionString();

        await using var rawReader = await OpenReaderAsync(
            """
            select
                cast(1 as number(10, 0)) as "id",
                cast(1.3 as number(10, 3)) as "dec",
                cast('Moscow' as varchar2(30)) as "name"
            from dual
            where 1 = 0
            """);
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "dec", "name"],
            types: [DataType.Number, DataType.Number, DataType.Text],
            rows: []);
    }

    [Test]
    [DisplayName("Oracle aliases возвращают uppercase без кавычек и точное имя с кавычками")]
    public async Task Aliases_return_uppercase_without_quotes_and_exact_name_with_quotes()
    {
        RequireOracleConnectionString();

        await using var rawReader = await OpenReaderAsync(
            """
            select
                cast(1 as number(10, 0)) as IdValue,
                'Moscow' as "CityName"
            from dual
            """);
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["IDVALUE", "CityName"],
            types: [DataType.Number, DataType.Text],
            rows: [
                (1m, "Moscow")
            ]);
        await Assert.That(() => reader.GetOrdinal("IdValue"))
            .ThrowsExactly<IndexOutOfRangeException>()
            .WithMessage("Column 'IdValue' was not found.");
        await Assert.That(() => reader.GetOrdinal("cityname"))
            .ThrowsExactly<IndexOutOfRangeException>()
            .WithMessage("Column 'cityname' was not found.");
    }

    [Test]
    [DisplayName("Oracle несколько строк читаются в порядке результата")]
    public async Task Reads_multiple_rows_in_result_order()
    {
        RequireOracleConnectionString();

        await using var rawReader = await OpenReaderAsync(
            """
            select cast(1 as number(10, 0)) as "id", 'first' as "name" from dual
            union all
            select cast(2 as number(10, 0)) as "id", 'second' as "name" from dual
            union all
            select cast(3 as number(10, 0)) as "id", 'third' as "name" from dual
            order by "id"
            """);
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "name"],
            types: [DataType.Number, DataType.Text],
            rows: [
                (1m, "first"),
                (2m, "second"),
                (3m, "third")
            ]);
    }

    [Test]
    [DisplayName("Oracle provider работает с Where поверх Domain reader")]
    public async Task Supports_where_over_domain_reader()
    {
        RequireOracleConnectionString();

        await using var rawReader = await OpenReaderAsync(
            """
            select cast(1 as number(10, 0)) as "id", 'Moscow' as "city" from dual
            union all
            select cast(2 as number(10, 0)) as "id", 'London' as "city" from dual
            union all
            select cast(3 as number(10, 0)) as "id", 'Moscow' as "city" from dual
            order by "id"
            """);
        await using var reader = rawReader
            .Normalize()
            .Where(row => row.Text("city") == "Moscow" && row.Number("id") > 1m);

        await Assert.That(reader).HaveData(
            columns: ["id", "city"],
            types: [DataType.Number, DataType.Text],
            rows: [
                (3m, "Moscow")
            ]);
    }

    [Test]
    [DisplayName("Oracle GetDataTypeName сохраняет доступным исходное имя типа")]
    public async Task Keeps_origin_data_type_name_available()
    {
        RequireOracleConnectionString();

        await using var rawReader = await OpenReaderAsync(
            """
            select
                cast('Moscow' as varchar2(30)) as "text_value",
                cast(1 as number(10, 0)) as "number_value"
            from dual
            """);
        await using var reader = rawReader.Normalize();

        await Assert.That(string.IsNullOrWhiteSpace(reader.GetDataTypeName(0))).IsFalse();
        await Assert.That(string.IsNullOrWhiteSpace(reader.GetDataTypeName(1))).IsFalse();
    }

    [Test]
    [DisplayName("Oracle null значение возвращает DBNull и сохраняет тип схемы")]
    public async Task Null_value_returns_dbnull()
    {
        RequireOracleConnectionString();

        await using var rawReader = await OpenReaderAsync("select cast(null as number(10, 0)) as \"value\" from dual");
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["value"],
            types: [DataType.Number],
            rows: [
                ValueTuple.Create(DBNull.Value)
            ]);
    }

    [Test]
    [DisplayName("Oracle SELECT 1 без alias использует имя колонки от Oracle")]
    public async Task Select_without_alias_uses_oracle_generated_column_name()
    {
        RequireOracleConnectionString();

        await using var rawReader = await OpenReaderAsync("select 1 from dual");
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["1"],
            types: [DataType.Number],
            rows: [
                ValueTuple.Create(1m)
            ]);
    }

    [Test]
    [DisplayName("Oracle ошибка запроса оборачивается в DbExecutionException")]
    public async Task Query_error_is_wrapped_in_provider_exception()
    {
        RequireOracleConnectionString();

        await Assert.That(async () => await OpenReaderAsync("select * from table_that_does_not_exist"))
            .ThrowsExactly<DbExecutionException>()
            .WithMessage("Database query failed for provider 'oracle': select * from table_that_does_not_exist");
    }

    [Test]
    [DisplayName("Oracle слишком большой numeric при чтении оборачивается в DataReaderValueException")]
    public async Task Oversized_numeric_value_error_is_wrapped_in_reader_value_exception()
    {
        RequireOracleConnectionString();

        await using var rawReader = await OpenReaderAsync(
            "select cast(99999999999999999999999999999999999999 as number(38, 0)) as \"value\" from dual");
        await using var reader = rawReader.Normalize();

        await Assert.That(() => reader.Read())
            .ThrowsExactly<DataReaderValueException>()
            .WithMessage("Failed to read field 'value' at ordinal 0.");
    }

    [Test]
    [DisplayName("Oracle повторяющиеся имена колонок кидают явную ошибку схемы")]
    public async Task Duplicate_column_names_throw_schema_exception()
    {
        RequireOracleConnectionString();

        await using var rawReader = await OpenReaderAsync("select 1 as value, 2 as value from dual");

        await Assert.That(() => rawReader.Normalize())
            .ThrowsExactly<DuplicateDataFieldNameException>()
            .WithMessage("Column name 'VALUE' is duplicated.");
    }

    [Test]
    [DisplayName("Oracle CollectMeta читает decimal precision и scale из column schema")]
    public async Task Collect_meta_reads_decimal_precision_and_scale_from_column_schema()
    {
        RequireOracleConnectionString();

        var meta = new DataMetaContainer();
        await using var rawReader = await OpenReaderAsync("select cast(12.34 as number(10, 2)) as \"amount\" from dual");
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
        await Assert.That(meta.Columns[0].DecimalPrecision).IsEqualTo(10);
        await Assert.That(meta.Columns[0].DecimalScale).IsEqualTo(2);
    }

    public static IEnumerable<(string SqlExpression, DataType ExpectedType, object Expected)> SqlValueCases()
    {
        yield return ("cast('example' as varchar2(32))", DataType.Text, "example");
        yield return ("cast('example' as nvarchar2(32))", DataType.Text, "example");
        yield return ("cast('x' as char(1))", DataType.Text, "x");
        yield return ("to_clob('example')", DataType.Text, "example");
        yield return ("hextoraw('DEADBEEF')", DataType.Text, "\\xdeadbeef");

        yield return ("cast(1 as number(5, 0))", DataType.Number, 1m);
        yield return ("cast(2 as integer)", DataType.Number, 2m);
        yield return ("cast(3 as number(19, 0))", DataType.Number, 3m);
        yield return ("cast(1.5 as binary_float)", DataType.Number, 1.5f);
        yield return ("cast(2.25 as binary_double)", DataType.Number, 2.25d);
        yield return ("cast(12345.6789 as number)", DataType.Number, 12345.6789m);
        yield return ("cast(9 as number(1, 0))", DataType.Number, 9m);
        yield return ("cast(1234567890 as number(10, 0))", DataType.Number, 1234567890m);
        yield return ("cast(0.1234567890 as number(10, 10))", DataType.Number, 0.1234567890m);
        yield return ("cast(12345.6789 as number(12, 4))", DataType.Number, 12345.6789m);

        yield return ("timestamp '2026-01-02 03:04:05'", DataType.DateTime, new DateTime(2026, 1, 2, 3, 4, 5));
        yield return ("date '2026-01-02'", DataType.DateTime, new DateTime(2026, 1, 2));
        yield return ("to_timestamp('2026-01-02 03:04:05', 'YYYY-MM-DD HH24:MI:SS')", DataType.DateTime, new DateTime(2026, 1, 2, 3, 4, 5));

        yield return ("case when 1 = 1 then 'true' else 'false' end", DataType.Text, "true");
        yield return ("interval '03:04:05' hour to second", DataType.Text, "+00 03:04:05.000000");
        yield return ("timestamp '2026-01-02 03:04:05' at time zone 'UTC'", DataType.Text, "02-JAN-26 03.04.05.000000 AM UTC");
    }

    private static void RequireOracleConnectionString()
    {
        Skip.When(
            string.IsNullOrWhiteSpace(GetConnectionString()),
            $"Set {ConnectionStringEnvironmentVariable} to run Oracle integration tests.");
    }

    private static string? GetConnectionString()
    {
        return Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
    }

    private static ValueTask<DbDataReader> OpenReaderAsync(string sql)
    {
        return Provider.OpenReaderAsync(
            new ConnectionStringSource
            {
                ConnectionString = GetConnectionString()
                    ?? throw new InvalidOperationException($"{ConnectionStringEnvironmentVariable} is not set.")
            },
            new SqlTableConfig
            {
                Sql = sql
            });
    }
}
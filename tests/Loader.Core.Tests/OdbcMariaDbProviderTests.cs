using System.Data;
using System.Data.Common;
using Loader.Core.Decorators;
using Loader.Core.Providers;
using Loader.Core.Providers.Odbc;
using Loader.Core.Providers.Sql;
using Loader.Core.Sources;
using Loader.Core.Tests.Infrastructure;

namespace Loader.Core.Tests;

[TestWithDependency(DatabaseDependency.OdbcMariaDb)]
public sealed class OdbcMariaDbProviderTests
{
    private static readonly OdbcProvider Provider = new();
    private readonly OdbcMariaDbTestDatabase database;

    public OdbcMariaDbProviderTests(OdbcMariaDbTestDatabase database)
    {
        this.database = database;
    }

    [Test]
    [MethodDataSource(nameof(SqlValueCases))]
    [DisplayName("MariaDB через ODBC sql-выражение выдает ожидаемый canonical value")]
    public async Task Sql_expression_maps_to_expected_value(string sqlExpression, DataType expectedType, object expected)
    {
        await using var rawReader = await OpenSharedReaderAsync($"select {sqlExpression} as value");
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["value"],
            types: [expectedType],
            rows: [
                ValueTuple.Create(expected)
            ]);
    }

    [Test]
    [DisplayName("MariaDB через ODBC пустой результат сохраняет имена и типы схемы")]
    public async Task Empty_result_preserves_schema()
    {
        await using var rawReader = await OpenSharedReaderAsync(
            """
            select
                cast(1 as signed) as id,
                cast(12.34 as decimal(10, 2)) as amount,
                cast('Moscow' as char(30)) as city,
                cast('2026-01-02' as date) as created
            where false
            """);
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "amount", "city", "created"],
            types: [DataType.Integer, DataType.Number, DataType.Text, DataType.Date],
            rows: []);
    }

    [Test]
    [DisplayName("MariaDB через ODBC aliases сохраняют имя результата запроса")]
    public async Task Aliases_return_result_column_names()
    {
        await using var rawReader = await OpenSharedReaderAsync(
            """
            select
                cast(1 as signed) as IdValue,
                cast('Moscow' as char(30)) as CityName
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
    [DisplayName("MariaDB через ODBC несколько строк читаются потоково в порядке результата")]
    public async Task Reads_multiple_rows_in_result_order()
    {
        await using var rawReader = await OpenSharedReaderAsync(
            """
            select 1 as id, 'first' as name
            union all select 2, 'second'
            union all select 3, 'third'
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
    [DisplayName("MariaDB через ODBC provider работает вместе с Where поверх Domain reader")]
    public async Task Supports_where_over_domain_reader()
    {
        await using var rawReader = await OpenSharedReaderAsync(
            """
            select 1 as id, 'Moscow' as city
            union all select 2, 'London'
            union all select 3, 'Moscow'
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
    [DisplayName("MariaDB через ODBC GetDataTypeName оставляет origin type name")]
    public async Task Keeps_origin_data_type_name_available()
    {
        await using var rawReader = await OpenSharedReaderAsync(
            """
            select
                cast(12.34 as decimal(10, 2)) as amount,
                cast('2026-01-02 03:04:05' as datetime) as created
            """);
        await using var reader = rawReader.Normalize();

        await Assert.That(reader.GetDataTypeName(0)).IsEqualTo("decimal");
        await Assert.That(reader.GetDataTypeName(1)).IsEqualTo("datetime");
    }

    [Test]
    [DisplayName("MariaDB через ODBC null значение выдает DBNull и сохраняет тип схемы")]
    public async Task Null_value_returns_dbnull()
    {
        await using var rawReader = await OpenSharedReaderAsync("select cast(null as signed) as value");
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["value"],
            types: [DataType.Integer],
            rows: [
                ValueTuple.Create(DBNull.Value)
            ]);
    }

    [Test]
    [DisplayName("MariaDB через ODBC Nullable базовых типов сохраняет базовый DataType и читает DBNull")]
    public async Task Nullable_base_types_preserve_schema_type_and_read_dbnull()
    {
        await using var rawReader = await OpenSharedReaderAsync(
            """
            select
                cast(null as char(30)) as text_value,
                cast(null as decimal(10, 2)) as amount_value,
                cast(null as datetime) as created_value,
                cast(null as date) as date_value,
                cast(null as time) as time_value
            """);
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["text_value", "amount_value", "created_value", "date_value", "time_value"],
            types: [DataType.Text, DataType.Number, DataType.DateTime, DataType.Date, DataType.Time],
            rows: [
                (DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value)
            ]);
    }

    [Test]
    [DisplayName("MariaDB через ODBC SELECT 1 без alias выдает имя колонки от MariaDB")]
    public async Task Select_without_alias_uses_mariadb_generated_column_name()
    {
        await using var rawReader = await OpenSharedReaderAsync("select 1");
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["1"],
            types: [DataType.Integer],
            rows: [
                ValueTuple.Create(1)
            ]);
    }

    [Test]
    [DisplayName("MariaDB через ODBC ошибка SQL запроса оборачивается в DbExecutionException с информацией о драйвере")]
    public async Task Query_error_is_wrapped_in_provider_exception_with_driver_info()
    {
        var exception = await Assert.That(async () => await OpenProviderReaderAsync("select * from table_that_does_not_exist"))
            .ThrowsExactly<DbExecutionException>();

        await Assert.That(exception!.Message).Contains("Database query failed for provider 'odbc'");
        await Assert.That(exception.Message).Contains("select * from table_that_does_not_exist");
        await Assert.That(exception.Data["OdbcDriverKind"]).IsEqualTo(OdbcDriverKind.MariaDb.ToString());
        await Assert.That(exception.Data["OdbcDriverName"]).IsEqualTo("maodbc.dll");
    }

    [Test]
    [DisplayName("MariaDB через ODBC повторяющиеся имена колонок кидают явную ошибку схемы")]
    public async Task Duplicate_column_names_throw_schema_exception()
    {
        await using var rawReader = await OpenSharedReaderAsync("select 1 as value, 2 as value");

        await Assert.That(() => rawReader.Normalize())
            .ThrowsExactly<DuplicateDataFieldNameException>()
            .WithMessage("Column name 'value' is duplicated.");
    }

    [Test]
    [DisplayName("MariaDB через ODBC CollectMeta сохраняет decimal precision и scale если драйвер их отдал")]
    public async Task Collect_meta_preserves_decimal_precision_and_scale_when_driver_provides_them()
    {
        var meta = new DataMetaContainer();
        await using var rawReader = await OpenSharedReaderAsync("select cast(12.34 as decimal(10, 2)) as amount");
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
        await AssertValidDecimalShape(meta.Columns[0].DecimalPrecision, meta.Columns[0].DecimalScale);
        await Assert.That(meta.Columns[0].DecimalScale).IsEqualTo(2);
    }

    [Test]
    [DisplayName("MariaDB через ODBC SUM decimal не возвращает invalid Decimal(0,0) shape")]
    public async Task Sum_decimal_does_not_expose_invalid_decimal_shape()
    {
        await using var rawReader = await OpenSharedReaderAsync(
            """
            select sum(amount) as total_amount
            from (
                select cast(12.34 as decimal(10, 2)) as amount
                union all select cast(20.01 as decimal(10, 2))
            ) as source_rows
            """);
        await using var reader = rawReader.Normalize();
        var field = reader.DataSchema.Fields[0];

        await Assert.That(field.DataType).IsEqualTo(DataType.Number);
        await Assert.That(field.ClrType).IsEqualTo(typeof(decimal));
        await AssertValidDecimalShape(field.NumericPrecision, field.NumericScale);
        await Assert.That(reader).HaveData(
            columns: ["total_amount"],
            types: [DataType.Number],
            rows: [
                ValueTuple.Create(32.35m)
            ]);
    }

    [Test]
    [MethodDataSource(nameof(UnsupportedSqlValueCases))]
    [DisplayName("MariaDB через ODBC явно неподдержанный тип выдает DBNull без чтения значения")]
    public async Task Unsupported_sql_expression_maps_to_dbnull(string sqlExpression)
    {
        await using var rawReader = await OpenSharedReaderAsync($"select {sqlExpression} as value");
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["value"],
            types: [DataType.Text],
            rows: [
                ValueTuple.Create(DBNull.Value)
            ]);
    }

    [Test]
    [DisplayName("MariaDB через ODBC читает типы, которые нельзя удобно проверить через CAST expression")]
    public async Task Table_only_types_map_to_expected_values()
    {
        var tableName = $"odbc_mariadb_types_{Guid.NewGuid():N}";
        await ExecuteSharedNonQueryAsync(
            $$"""
            create table {{tableName}} (
                text_value text,
                medium_text_value mediumtext,
                long_text_value longtext,
                year_value year,
                bit_value bit(1),
                binary_value binary(4),
                varbinary_value varbinary(16),
                blob_value blob
            )
            """);

        try
        {
            await ExecuteSharedNonQueryAsync(
                $$"""
                insert into {{tableName}} (
                    text_value,
                    medium_text_value,
                    long_text_value,
                    year_value,
                    bit_value,
                    binary_value,
                    varbinary_value,
                    blob_value
                )
                values (
                    'text',
                    'medium text',
                    'long text',
                    2026,
                    b'1',
                    0xDEADBEEF,
                    0xDEADBEEF,
                    0xDEADBEEF
                )
                """);

            await using var rawReader = await OpenSharedReaderAsync(
                $$"""
                select
                    text_value,
                    medium_text_value,
                    long_text_value,
                    year_value,
                    bit_value,
                    binary_value,
                    varbinary_value,
                    blob_value
                from {{tableName}}
                """);
            await using var reader = rawReader.Normalize();

            await Assert.That(reader).HaveData(
                columns: [
                    "text_value",
                    "medium_text_value",
                    "long_text_value",
                    "year_value",
                    "bit_value",
                    "binary_value",
                    "varbinary_value",
                    "blob_value"
                ],
                types: [
                    DataType.Text,
                    DataType.Text,
                    DataType.Text,
                    DataType.Integer,
                    DataType.Boolean,
                    DataType.Text,
                    DataType.Text,
                    DataType.Text
                ],
                rows: [
                    ("text", "medium text", "long text", 2026, true, DBNull.Value, DBNull.Value, DBNull.Value)
                ]);
        }
        finally
        {
            await ExecuteSharedNonQueryAsync($"drop table if exists {tableName}");
        }
    }

    public static IEnumerable<(string SqlExpression, DataType ExpectedType, object Expected)> SqlValueCases()
    {
        yield return ("cast('example' as char(32))", DataType.Text, "example");
        yield return ("cast('example' as varchar(32))", DataType.Text, "example");
        yield return ("cast('пример' as char(32))", DataType.Text, "пример");
        yield return ("cast('пример' as varchar(32))", DataType.Text, "пример");
        yield return ("cast(-128 as signed)", DataType.Integer, -128);
        yield return ("cast(127 as signed)", DataType.Integer, 127);
        yield return ("cast(255 as unsigned)", DataType.Integer, 255L);
        yield return ("cast(-32768 as signed)", DataType.Integer, -32768);
        yield return ("cast(2147483647 as signed)", DataType.Integer, 2147483647L);
        yield return ("cast(-2147483648 as signed)", DataType.Integer, -2147483648L);
        yield return ("cast(9223372036854775807 as signed)", DataType.Integer, 9223372036854775807L);
        yield return ("cast(12.34 as decimal(10, 2))", DataType.Number, 12.34m);
        yield return ("cast(1.5 as float)", DataType.Number, 1.5f);
        yield return ("cast(2.25 as double)", DataType.Number, 2.25d);
        yield return ("cast('2026-01-02 03:04:05' as datetime)", DataType.DateTime, new DateTime(2026, 1, 2, 3, 4, 5));
        yield return ("cast('2026-01-02' as date)", DataType.Date, new DateOnly(2026, 1, 2));
        yield return ("cast('03:04:05' as time)", DataType.Time, new TimeOnly(3, 4, 5));
        yield return ("cast('{\"city\":\"Moscow\"}' as char(32))", DataType.Text, "{\"city\":\"Moscow\"}");
    }

    public static IEnumerable<string> UnsupportedSqlValueCases()
    {
        yield return "cast(0xDEADBEEF as binary(4))";
    }

    private async ValueTask<DbDataReader> OpenSharedReaderAsync(string sql)
    {
        var command = database.Connection.CreateCommand();
        command.CommandText = sql;

        try
        {
            var reader = await command
                .ExecuteReaderAsync(CommandBehavior.SequentialAccess)
                .ConfigureAwait(false);

            return new OdbcTemporalDataReader(new CommandOwnedDataReader(reader, command));
        }
        catch
        {
            await command.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async Task ExecuteSharedNonQueryAsync(string sql)
    {
        await using var command = database.Connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private ValueTask<DbDataReader> OpenProviderReaderAsync(string sql)
    {
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

    private sealed class CommandOwnedDataReader : DbDataReaderDecorator
    {
        private readonly DbCommand command;

        public CommandOwnedDataReader(DbDataReader inner, DbCommand command)
            : base(inner)
        {
            this.command = command;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                command.Dispose();
            }
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync().ConfigureAwait(false);
            await command.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async Task AssertValidDecimalShape(int? precision, int? scale)
    {
        await Assert.That(precision is null or > 0).IsTrue();
        await Assert.That(scale is null or >= 0).IsTrue();
        await Assert.That(precision == 0 && scale == 0).IsFalse();
    }
}

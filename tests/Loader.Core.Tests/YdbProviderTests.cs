using System.Data.Common;
using Loader.Core.Providers;
using Loader.Core.Providers.Sql;
using Loader.Core.Providers.Ydb;
using Loader.Core.Sources;
using Loader.Core.Tests.Infrastructure;

namespace Loader.Core.Tests;

[TestWithDependency(DatabaseDependency.Ydb)]
public sealed class YdbProviderTests
{
    private static readonly YdbProvider Provider = new();
    private readonly YdbTestDatabase database;

    public YdbProviderTests(YdbTestDatabase database)
    {
        this.database = database;
    }

    [Test]
    [MethodDataSource(nameof(SqlValueCases))]
    [DisplayName("YDB sql-выражение выдает ожидаемый canonical value")]
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
    [DisplayName("YDB пустой результат сохраняет имена и типы схемы")]
    public async Task Empty_result_preserves_schema()
    {
        await using var rawReader = await OpenReaderAsync(
            """
            select id, amount, city, active from as_table([
                <|
                    id:Int32("1"),
                    amount:Decimal("12.34", 10, 2),
                    city:Utf8("Moscow"),
                    active:Bool("true")
                |>
            ])
            where false
            """);
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "amount", "city", "active"],
            types: [DataType.Integer, DataType.Number, DataType.Text, DataType.Boolean],
            rows: []);
    }

    [Test]
    [DisplayName("YDB aliases сохраняют имя результата запроса")]
    public async Task Aliases_return_result_column_names()
    {
        await using var rawReader = await OpenReaderAsync(
            """
            select
                Int32("1") as IdValue,
                Utf8("Moscow") as CityName
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
    [DisplayName("YDB несколько строк читаются потоково в порядке результата")]
    public async Task Reads_multiple_rows_in_result_order()
    {
        await using var rawReader = await OpenReaderAsync(
            """
            select id, name from as_table([
                <|id:Int32("1"), name:Utf8("first")|>,
                <|id:Int32("2"), name:Utf8("second")|>,
                <|id:Int32("3"), name:Utf8("third")|>
            ])
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
    [DisplayName("YDB provider работает вместе с Where поверх Domain reader")]
    public async Task Supports_where_over_domain_ydb_reader()
    {
        await using var rawReader = await OpenReaderAsync(
            """
            select id, city from as_table([
                <|id:Int32("1"), city:Utf8("Moscow")|>,
                <|id:Int32("2"), city:Utf8("London")|>,
                <|id:Int32("3"), city:Utf8("Moscow")|>
            ])
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
    [DisplayName("YDB GetDataTypeName оставляет origin type name")]
    public async Task Keeps_origin_data_type_name_available()
    {
        await using var rawReader = await OpenReaderAsync(
            """
            select
                Decimal("12.34", 10, 2) as amount,
                Datetime("2026-01-02T03:04:05Z") as created
            """);
        await using var reader = rawReader.Normalize();

        await Assert.That(reader.GetDataTypeName(0)).Contains("Decimal");
        await Assert.That(reader.GetDataTypeName(1)).Contains("Datetime");
    }

    [Test]
    [DisplayName("YDB null значение выдает DBNull и сохраняет тип схемы")]
    public async Task Null_value_returns_dbnull()
    {
        await using var rawReader = await OpenReaderAsync("select Nothing(Int32?) as value");
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["value"],
            types: [DataType.Integer],
            rows: [
                ValueTuple.Create(DBNull.Value)
            ]);
    }

    [Test]
    [DisplayName("YDB Nullable базовых типов сохраняет базовый DataType и читает DBNull")]
    public async Task Nullable_base_types_preserve_schema_type_and_read_dbnull()
    {
        await using var rawReader = await OpenReaderAsync(
            """
            select
                Nothing(Utf8?) as text_value,
                Nothing(Decimal(10, 2)?) as amount_value,
                Nothing(Datetime?) as created_value,
                Nothing(Bool?) as active_value
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
    [DisplayName("YDB SELECT 1 без alias выдает имя колонки от YDB")]
    public async Task Select_without_alias_uses_ydb_generated_column_name()
    {
        await using var rawReader = await OpenReaderAsync("select 1");
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["column0"],
            types: [DataType.Integer],
            rows: [
                ValueTuple.Create(1)
            ]);
    }

    [Test]
    [DisplayName("YDB ошибка SQL запроса оборачивается в DbExecutionException")]
    public async Task Query_error_is_wrapped_in_provider_exception()
    {
        await Assert.That(async () => await OpenReaderAsync("select * from table_that_does_not_exist"))
            .ThrowsExactly<DbExecutionException>()
            .WithMessage("Database query failed for provider 'ydb': select * from table_that_does_not_exist");
    }

    [Test]
    [DisplayName("YDB повторяющиеся имена колонок кидают явную ошибку схемы")]
    public async Task Duplicate_column_names_throw_schema_exception()
    {
        await using var rawReader = await OpenReaderAsync("select 1 as value, 2 as value");

        await Assert.That(() => rawReader.Normalize())
            .ThrowsExactly<DuplicateDataFieldNameException>()
            .WithMessage("Column name 'value' is duplicated.");
    }

    [Test]
    [DisplayName("YDB CollectMeta берет decimal precision и scale из column schema")]
    public async Task Collect_meta_reads_decimal_precision_and_scale_from_column_schema()
    {
        var meta = new DataMetaContainer();
        await using var rawReader = await OpenReaderAsync("select Decimal(\"12.34\", 10, 2) as amount");
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

    [Test]
    [DisplayName("YDB provider читает реальные строки из таблицы")]
    public async Task Reads_rows_from_physical_table()
    {
        var sourceTable = $"core_ydb_people_{Guid.NewGuid():N}";
        await database.ExecuteAsync($"DROP TABLE IF EXISTS {sourceTable};");
        await database.ExecuteAsync(
            $"""
            CREATE TABLE {sourceTable}
            (
                id Int32 NOT NULL,
                name Utf8,
                city Utf8,
                PRIMARY KEY(id)
            );
            """);
        await database.ExecuteAsync(
            $"""
            UPSERT INTO {sourceTable} (id, name, city) VALUES
            (1, Utf8("Alice"), Utf8("Moscow")),
            (2, Utf8("Bob"), Utf8("Berlin")),
            (3, Utf8("Charlie"), Utf8("London"));
            """);

        await using var rawReader = await OpenReaderAsync($"select id, name, city from {sourceTable} order by id");
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "name", "city"],
            types: [DataType.Integer, DataType.Text, DataType.Text],
            rows: [
                (1, "Alice", "Moscow"),
                (2, "Bob", "Berlin"),
                (3, "Charlie", "London")
            ]);
    }

    [Test]
    [DisplayName("YDB schema отличает nullable и not-null поля таблицы")]
    public async Task Table_schema_preserves_nullable_flags()
    {
        var sourceTable = $"core_ydb_nullable_{Guid.NewGuid():N}";
        await database.ExecuteAsync($"DROP TABLE IF EXISTS {sourceTable};");
        await database.ExecuteAsync(
            $"""
            CREATE TABLE {sourceTable}
            (
                id Int32 NOT NULL,
                amount Decimal(10, 2),
                active Bool,
                PRIMARY KEY(id)
            );
            """);
        await database.ExecuteAsync(
            $"""
            UPSERT INTO {sourceTable} (id, amount, active) VALUES
            (1, Decimal("10.50", 10, 2), Bool("true")),
            (2, NULL, NULL);
            """);

        await using var rawReader = await OpenReaderAsync($"select id, amount, active from {sourceTable} order by id");
        await using var reader = rawReader.Normalize();

        await Assert.That(reader.DataSchema.Fields[0].AllowDBNull).IsFalse();
        await Assert.That(reader.DataSchema.Fields[1].AllowDBNull).IsTrue();
        await Assert.That(reader.DataSchema.Fields[2].AllowDBNull).IsTrue();
        await Assert.That(reader).HaveData(
            columns: ["id", "amount", "active"],
            types: [DataType.Integer, DataType.Number, DataType.Boolean],
            rows: [
                (1, 10.50m, true),
                (2, DBNull.Value, DBNull.Value)
            ]);
    }

    [Test]
    [DisplayName("YDB GetColumnSchema возвращает ожидаемые имена и CLR-типы базовых колонок")]
    public async Task Column_schema_contains_expected_names_and_clr_types()
    {
        await using var rawReader = await OpenReaderAsync(
            """
            select
                Int64("-4") as id,
                Decimal("12.34", 10, 2) as amount,
                Utf8("Moscow") as city,
                Bool("true") as active,
                Datetime("2026-01-02T03:04:05Z") as created
            """);

        var columns = rawReader.GetColumnSchema();

        await Assert.That(columns.Select(static column => column.ColumnName).ToArray())
            .IsEquivalentTo(["id", "amount", "city", "active", "created"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(columns[0].DataType).IsEqualTo(typeof(long));
        await Assert.That(columns[1].DataType).IsEqualTo(typeof(decimal));
        await Assert.That(columns[2].DataType).IsEqualTo(typeof(string));
        await Assert.That(columns[3].DataType).IsEqualTo(typeof(bool));
        await Assert.That(columns[4].DataType).IsEqualTo(typeof(DateTime));
    }

    public static IEnumerable<(string SqlExpression, DataType ExpectedType, object Expected)> SqlValueCases()
    {
        yield return ("""Bool("true")""", DataType.Boolean, true);
        yield return ("""Int8("-1")""", DataType.Integer, (sbyte)-1);
        yield return ("""Int16("-2")""", DataType.Integer, (short)-2);
        yield return ("""Int32("-3")""", DataType.Integer, -3);
        yield return ("""Int64("-4")""", DataType.Integer, -4L);
        yield return ("""Uint8("1")""", DataType.Integer, (byte)1);
        yield return ("""Uint16("2")""", DataType.Integer, (ushort)2);
        yield return ("""Uint32("3")""", DataType.Integer, 3U);
        yield return ("""Uint64("4")""", DataType.Integer, 4UL);
        yield return ("""Float("-5")""", DataType.Number, -5f);
        yield return ("""Double("6")""", DataType.Number, 6d);
        yield return ("""Decimal("1.23", 5, 2)""", DataType.Number, 1.23m);
        yield return ("""String("foo")""", DataType.Text, "foo");
        yield return ("""Utf8("hello")""", DataType.Text, "hello");
        yield return ("""Yson("<a=1>[3;%false]")""", DataType.Text, "<a=1>[3;%false]");
        yield return ("""Json(@@{"a":1,"b":null}@@)""", DataType.Text, """{"a":1,"b":null}""");
        yield return ("""Date("2017-11-27")""", DataType.DateTime, new DateTime(2017, 11, 27));
        yield return ("""Datetime("2017-11-27T13:24:00Z")""", DataType.DateTime, new DateTime(2017, 11, 27, 13, 24, 0));
        yield return ("""Timestamp("2017-11-27T13:24:00.123456Z")""", DataType.DateTime, new DateTime(2017, 11, 27, 13, 24, 0, 123).AddTicks(4560));
        yield return ("""Interval("P1D")""", DataType.Text, "P1D");
        yield return ("""TzDate("2017-11-27,Europe/Moscow")""", DataType.Text, "2017-11-27,Europe/Moscow");
        yield return ("""TzDatetime("2017-11-27T13:24:00,Europe/Moscow")""", DataType.Text, "2017-11-27T13:24:00,Europe/Moscow");
        yield return ("""TzTimestamp("2017-11-27T13:24:00.123456,Europe/Moscow")""", DataType.Text, "2017-11-27T13:24:00.123456,Europe/Moscow");
        yield return ("""Uuid("f9d5cc3f-f1dc-4d9c-b97e-766e57ca4ccb")""", DataType.Text, "f9d5cc3f-f1dc-4d9c-b97e-766e57ca4ccb");
        yield return ("""Bytes("foo")""", DataType.Text, "foo");
    }

    private ValueTask<DbDataReader> OpenReaderAsync(string sql)
    {
        return Provider.OpenReaderAsync(
            new ConnectionStringSource
            {
                ConnectionString = database.ConnectionString
            },
            new SqlTableConfig { Sql = sql });
    }
}

using System.Data;
using Loader.Core.Data;
using Loader.Core.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Loader.Core.Tests;

public sealed class WhereDomainDataReaderTests
{
    [Test]
    [DisplayName("Where с Text по имени оставляет только строки прошедшие predicate")]
    public async Task Filters_rows_by_text_column()
    {
        using var table = CreateTable(
            ("id", typeof(int)),
            ("name", typeof(string)));
        table.Rows.Add(1, "Moscow");
        table.Rows.Add(2, "London");
        table.Rows.Add(3, "MOSCOW");

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader
            .Normalize()
            .Where(row => string.Equals(row.Text("name"), "moscow", StringComparison.OrdinalIgnoreCase));


        await Assert.That(reader).HaveData(
            columns: ["id", "name"],
            types: [DataType.Integer, DataType.Text],
            rows: [
                (1, "Moscow"),
                (3, "MOSCOW")
            ]);
    }

    [Test]
    [DisplayName("Where без подходящих строк сохраняет схему и возвращает пустой поток")]
    public async Task Empty_result_preserves_schema()
    {
        using var table = CreateTable(
            ("id", typeof(int)),
            ("name", typeof(string)));
        table.Rows.Add(1, "Moscow");
        table.Rows.Add(2, "London");

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader
            .Normalize()
            .Where(row => row.Text("name") == "Berlin");

        await Assert.That(reader).HaveData(
            columns: ["id", "name"],
            types: [DataType.Integer, DataType.Text],
            rows: []);
    }

    [Test]
    [DisplayName("Where с DBNull в Text видит null и возвращает DBNull в данных")]
    public async Task Text_returns_null_for_db_null()
    {
        using var table = CreateTable(("name", typeof(string)));
        table.Rows.Add(DBNull.Value);
        table.Rows.Add("Moscow");

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader
            .Normalize()
            .Where(row => row.Text("name") is null);

        await Assert.That(reader).HaveData(
            columns: ["name"],
            types: [DataType.Text],
            rows: [
                ValueTuple.Create(DBNull.Value)
            ]);
    }

    [Test]
    [DisplayName("Where с другим регистром имени поля кидает ошибку поиска колонки")]
    public async Task Field_name_lookup_is_case_sensitive()
    {
        using var table = CreateTable(("name", typeof(string)));
        table.Rows.Add("Moscow");

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader
            .Normalize()
            .Where(row => row.Text("NAME") == "Moscow");

        await Assert.That(() => reader.Read())
            .ThrowsExactly<IndexOutOfRangeException>()
            .WithMessage("Column 'NAME' was not found.");
    }

    [Test]
    [DisplayName("Row typed-accessors для DBNull возвращают null")]
    public async Task Row_typed_accessors_return_null_for_db_null()
    {
        using var table = CreateTable(
            ("text", typeof(string)),
            ("number", typeof(decimal)),
            ("integer", typeof(int)),
            ("boolean", typeof(bool)),
            ("datetime", typeof(DateTime)),
            ("date", typeof(DateOnly)),
            ("time", typeof(TimeSpan)));
        table.Rows.Add(
            DBNull.Value,
            DBNull.Value,
            DBNull.Value,
            DBNull.Value,
            DBNull.Value,
            DBNull.Value,
            DBNull.Value);
        var allValuesAreNull = false;

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader
            .Normalize()
            .Where(row =>
            {
                allValuesAreNull =
                    row.Text("text") is null
                    && row.Number("number") is null
                    && row.Integer("integer") is null
                    && row.Boolean("boolean") is null
                    && row.DateTime("datetime") is null
                    && row.Date("date") is null
                    && row.Time("time") is null;

                return allValuesAreNull;
            });

        await Assert.That(reader).HaveData(
            columns: ["text", "number", "integer", "boolean", "datetime", "date", "time"],
            types: [DataType.Text, DataType.Number, DataType.Integer, DataType.Boolean, DataType.DateTime, DataType.Date, DataType.Time],
            rows: [
                (DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value)
            ]);
        await Assert.That(allValuesAreNull).IsTrue();
    }

    [Test]
    [DisplayName("Where с Number и Boolean фильтрует по typed значениям")]
    public async Task Filters_by_number_and_boolean_values()
    {
        using var table = CreateTable(
            ("id", typeof(int)),
            ("amount", typeof(decimal)),
            ("active", typeof(bool)));
        table.Rows.Add(1, 10.50m, true);
        table.Rows.Add(2, 20.00m, false);
        table.Rows.Add(3, 30.25m, true);

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader
            .Normalize()
            .Where(row => row.Number("amount") > 20m && row.Boolean("active") == true);

        await Assert.That(reader).HaveData(
            columns: ["id", "amount", "active"],
            types: [DataType.Integer, DataType.Number, DataType.Boolean],
            rows: [
                (3, 30.25m, true)
            ]);
    }

    [Test]
    [DisplayName("Row Integer для разных CLR integer типов возвращает long и не меняет значения reader")]
    public async Task Row_integer_converts_integer_clr_types_to_long()
    {
        using var table = CreateTable(
            ("byte_value", typeof(byte)),
            ("sbyte_value", typeof(sbyte)),
            ("short_value", typeof(short)),
            ("ushort_value", typeof(ushort)),
            ("int_value", typeof(int)),
            ("uint_value", typeof(uint)),
            ("long_value", typeof(long)),
            ("ulong_value", typeof(ulong)));
        table.Rows.Add((byte)1, (sbyte)2, (short)3, (ushort)4, 5, 6U, 7L, 8UL);
        long?[] actualValues = [];

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader
            .Normalize()
            .Where(row =>
            {
                actualValues =
                [
                    row.Integer("byte_value"),
                    row.Integer("sbyte_value"),
                    row.Integer("short_value"),
                    row.Integer("ushort_value"),
                    row.Integer("int_value"),
                    row.Integer("uint_value"),
                    row.Integer("long_value"),
                    row.Integer("ulong_value")
                ];

                return true;
            });

        await Assert.That(reader).HaveData(
            columns: ["byte_value", "sbyte_value", "short_value", "ushort_value", "int_value", "uint_value", "long_value", "ulong_value"],
            types: [DataType.Integer, DataType.Integer, DataType.Integer, DataType.Integer, DataType.Integer, DataType.Integer, DataType.Integer, DataType.Integer],
            rows: [
                ((byte)1, (sbyte)2, (short)3, (ushort)4, 5, 6U, 7L, 8UL)
            ]);
        await Assert.That(actualValues[0]).IsEqualTo(1L);
        await Assert.That(actualValues[1]).IsEqualTo(2L);
        await Assert.That(actualValues[2]).IsEqualTo(3L);
        await Assert.That(actualValues[3]).IsEqualTo(4L);
        await Assert.That(actualValues[4]).IsEqualTo(5L);
        await Assert.That(actualValues[5]).IsEqualTo(6L);
        await Assert.That(actualValues[6]).IsEqualTo(7L);
        await Assert.That(actualValues[7]).IsEqualTo(8L);
    }

    [Test]
    [DisplayName("Row Number для разных CLR number типов возвращает decimal и не меняет значения reader")]
    public async Task Row_number_converts_number_clr_types_to_decimal()
    {
        using var table = CreateTable(
            ("float_value", typeof(float)),
            ("double_value", typeof(double)),
            ("decimal_value", typeof(decimal)));
        table.Rows.Add(1.25f, 2.50d, 3.75m);
        decimal?[] actualValues = [];

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader
            .Normalize()
            .Where(row =>
            {
                actualValues =
                [
                    row.Number("float_value"),
                    row.Number("double_value"),
                    row.Number("decimal_value")
                ];

                return true;
            });

        await Assert.That(reader).HaveData(
            columns: ["float_value", "double_value", "decimal_value"],
            types: [DataType.Number, DataType.Number, DataType.Number],
            rows: [
                (1.25f, 2.50d, 3.75m)
            ]);
        await Assert.That(actualValues[0]).IsEqualTo(1.25m);
        await Assert.That(actualValues[1]).IsEqualTo(2.50m);
        await Assert.That(actualValues[2]).IsEqualTo(3.75m);
    }

    [Test]
    [DisplayName("Where в цепочке применяет predicates последовательно")]
    public async Task Chains_multiple_where_readers()
    {
        using var table = CreateTable(
            ("id", typeof(int)),
            ("city", typeof(string)));
        table.Rows.Add(1, "Moscow");
        table.Rows.Add(2, "Moscow");
        table.Rows.Add(3, "London");

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader
            .Normalize()
            .Where(row => row.Text("city") == "Moscow")
            .Where(row => row.Integer("id") > 1);

        await Assert.That(reader).HaveData(
            columns: ["id", "city"],
            types: [DataType.Integer, DataType.Text],
            rows: [
                (2, "Moscow")
            ]);
    }

    [Test]
    [DisplayName("Where вызывает predicate на каждой исходной строке при чтении до конца")]
    public async Task Calls_predicate_for_each_source_row()
    {
        using var table = CreateTable(("id", typeof(int)));
        table.Rows.Add(1);
        table.Rows.Add(2);
        table.Rows.Add(3);
        var calls = 0;

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader
            .Normalize()
            .Where(row =>
            {
                calls++;
                return row.Integer("id") > 1;
            });

        await Assert.That(reader).HaveData(
            columns: ["id"],
            types: [DataType.Integer],
            rows: [
                ValueTuple.Create(2),
                ValueTuple.Create(3)
            ]);
        await Assert.That(calls).IsEqualTo(3);
    }

    [Test]
    [DisplayName("Where через ReadAsync пропускает строки пока predicate не пройдет")]
    public async Task Read_async_filters_rows()
    {
        using var table = CreateTable(("id", typeof(int)));
        table.Rows.Add(1);
        table.Rows.Add(2);

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader
            .Normalize()
            .Where(row => row.Integer("id") == 2);

        await Assert.That(await reader.ReadAsync()).IsTrue();
        await Assert.That(reader.GetValue(0)).IsEqualTo(2);
        await Assert.That(await reader.ReadAsync()).IsFalse();
    }

    private static DataTable CreateTable(params (string Name, Type Type)[] columns)
    {
        var table = new DataTable();
        foreach (var column in columns)
        {
            table.Columns.Add(column.Name, column.Type);
        }

        return table;
    }
}

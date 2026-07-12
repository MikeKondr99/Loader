using System.Data;
using System.Data.Common;
using Loader.Core.Tests.Infrastructure;

namespace Loader.Core.Tests;

[DisplayName("Автокаст трансформация")]
public sealed class AutoCastDataReaderTests
{
    [Test]
    [DisplayName("AutoCast применяет явную схему и меняет типы значений")]
    public async Task Applies_explicit_schema_and_changes_value_types()
    {
        using var table = CreateTextTable("id", "amount", "active", "created", "date", "time");
        table.Rows.Add("42", "10.50", "true", "2026-01-02 03:04:05", "2026-01-02", "03:04:05");

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader
            .Normalize()
            .AutoCast(new AutoCastSchema
            {
                Fields =
                [
                    new AutoCastField { Name = "id", Format = AutoCastFormats.Integer },
                    new AutoCastField { Name = "amount", Format = AutoCastFormats.InvariantNumber },
                    new AutoCastField { Name = "active", Format = AutoCastFormats.Boolean },
                    new AutoCastField { Name = "created", Format = AutoCastFormats.DateTimeExact("yyyy-MM-dd HH:mm:ss") },
                    new AutoCastField { Name = "date", Format = AutoCastFormats.DateExact("yyyy-MM-dd") },
                    new AutoCastField { Name = "time", Format = AutoCastFormats.TimeExact("HH:mm:ss") }
                ]
            });

        await Assert.That(reader).HaveData(
            columns: ["id", "amount", "active", "created", "date", "time"],
            types: [DataType.Integer, DataType.Number, DataType.Boolean, DataType.DateTime, DataType.Date, DataType.Time],
            rows: [
                (42L, 10.50m, true, new DateTime(2026, 1, 2, 3, 4, 5), new DateOnly(2026, 1, 2), new TimeOnly(3, 4, 5))
            ]);
    }

    [Test]
    [DisplayName("AutoCast typed getters читают сконвертированные значения")]
    public async Task Typed_getters_read_converted_values()
    {
        using var table = CreateTextTable("id", "amount", "active", "created", "date", "time");
        table.Rows.Add("42", "10.50", "true", "2026-01-02 03:04:05", "2026-01-02", "03:04:05");

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader
            .Normalize()
            .AutoCast(new AutoCastSchema
            {
                Fields =
                [
                    new AutoCastField { Name = "id", Format = AutoCastFormats.Integer },
                    new AutoCastField { Name = "amount", Format = AutoCastFormats.InvariantNumber },
                    new AutoCastField { Name = "active", Format = AutoCastFormats.Boolean },
                    new AutoCastField { Name = "created", Format = AutoCastFormats.DateTimeExact("yyyy-MM-dd HH:mm:ss") },
                    new AutoCastField { Name = "date", Format = AutoCastFormats.DateExact("yyyy-MM-dd") },
                    new AutoCastField { Name = "time", Format = AutoCastFormats.TimeExact("HH:mm:ss") }
                ]
            });

        await Assert.That(reader.Read()).IsTrue();
        await Assert.That(reader.GetInt64(0)).IsEqualTo(42L);
        await Assert.That(reader.GetDecimal(1)).IsEqualTo(10.50m);
        await Assert.That(reader.GetBoolean(2)).IsTrue();
        await Assert.That(reader.GetDateTime(3)).IsEqualTo(new DateTime(2026, 1, 2, 3, 4, 5));
        await Assert.That(reader.GetFieldValue<DateOnly>(4)).IsEqualTo(new DateOnly(2026, 1, 2));
        await Assert.That(reader.GetFieldValue<TimeOnly>(5)).IsEqualTo(new TimeOnly(3, 4, 5));
        await Assert.That(() => reader.GetInt32(0))
            .ThrowsExactly<InvalidCastException>()
            .WithMessage("Column 'id' at ordinal 0 has CLR type 'System.Int64' and cannot be read with accessor 'GetInt32'.");
    }

    [Test]
    [DisplayName("AutoCast обновляет GetColumnSchema и GetSchemaTable")]
    public async Task Updates_ado_schema_metadata()
    {
        using var table = CreateTextTable("id");
        table.Rows.Add("42");

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader
            .Normalize()
            .AutoCast(new AutoCastSchema
            {
                Fields =
                [
                    new AutoCastField { Name = "id", Format = AutoCastFormats.Integer }
                ]
            });

        var column = reader.GetColumnSchema().Single();
        var schemaTable = reader.GetSchemaTable();

        await Assert.That(column.ColumnName).IsEqualTo("id");
        await Assert.That(column.DataType).IsEqualTo(typeof(long));
        await Assert.That((Type)schemaTable.Rows[0][SchemaTableColumn.DataType]).IsEqualTo(typeof(long));
    }

    [Test]
    [DisplayName("AutoCast не указанные в схеме поля оставляет без изменений")]
    public async Task Leaves_fields_not_present_in_schema_unchanged()
    {
        using var table = CreateTextTable("id", "name");
        table.Rows.Add("42", "Moscow");

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader
            .Normalize()
            .AutoCast(new AutoCastSchema
            {
                Fields =
                [
                    new AutoCastField { Name = "id", Format = AutoCastFormats.Integer }
                ]
            });

        await Assert.That(reader).HaveData(
            columns: ["id", "name"],
            types: [DataType.Integer, DataType.Text],
            rows: [
                (42L, "Moscow")
            ]);
    }

    [Test]
    [DisplayName("AutoCast DBNull оставляет DBNull")]
    public async Task Keeps_dbnull_values_as_dbnull()
    {
        using var table = CreateTextTable("id", "amount");
        table.Rows.Add(DBNull.Value, DBNull.Value);

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader
            .Normalize()
            .AutoCast(new AutoCastSchema
            {
                Fields =
                [
                    new AutoCastField { Name = "id", Format = AutoCastFormats.Integer },
                    new AutoCastField { Name = "amount", Format = AutoCastFormats.InvariantNumber }
                ]
            });

        await Assert.That(reader).HaveData(
            columns: ["id", "amount"],
            types: [DataType.Integer, DataType.Number],
            rows: [
                (DBNull.Value, DBNull.Value)
            ]);
    }

    [Test]
    [DisplayName("AutoCast explicit schema не меняет уже типизированное поле")]
    public async Task Explicit_schema_does_not_cast_already_typed_field()
    {
        using var table = new DataTable();
        table.Columns.Add("id", typeof(int));
        table.Columns.Add("amount", typeof(string));
        table.Rows.Add(42, "10.50");

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader
            .Normalize()
            .AutoCast(new AutoCastSchema
            {
                Fields =
                [
                    new AutoCastField { Name = "id", Format = AutoCastFormats.Text },
                    new AutoCastField { Name = "amount", Format = AutoCastFormats.InvariantNumber }
                ]
            });

        await Assert.That(reader).HaveData(
            columns: ["id", "amount"],
            types: [DataType.Integer, DataType.Number],
            rows: [
                (42, 10.50m)
            ]);
    }

    [Test]
    [DisplayName("AutoCast ошибка преобразования оборачивается в DataReaderValueException")]
    public async Task Conversion_error_is_thrown_when_field_is_read()
    {
        using var table = CreateTextTable("id");
        table.Rows.Add("not-int");

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader
            .Normalize()
            .AutoCast(new AutoCastSchema
            {
                Fields =
                [
                    new AutoCastField { Name = "id", Format = AutoCastFormats.Integer }
                ]
            });

        await Assert.That(reader.Read()).IsTrue();
        await Assert.That(() => reader.GetValue(0))
            .ThrowsExactly<DataReaderValueException>()
            .WithMessage("Failed to read field 'id' at ordinal 0.");
    }

    [Test]
    [DisplayName("AutoCast Read не конвертирует неиспользованное поле")]
    public async Task Read_does_not_convert_unused_field()
    {
        using var table = CreateTextTable("id", "name");
        table.Rows.Add("not-int", "Moscow");

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader
            .Normalize()
            .AutoCast(new AutoCastSchema
            {
                Fields =
                [
                    new AutoCastField { Name = "id", Format = AutoCastFormats.Integer }
                ]
            });

        await Assert.That(reader.Read()).IsTrue();
        await Assert.That(reader.GetValue(1)).IsEqualTo("Moscow");
    }

    [Test]
    [DisplayName("AutoCast schema с неизвестным полем падает сразу")]
    public async Task Unknown_schema_field_throws_immediately()
    {
        using var table = CreateTextTable("id");

        using var rawReader = table.CreateDataReader();
        using var reader = rawReader.Normalize();

        await Assert.That(() => reader.AutoCast(new AutoCastSchema
            {
                Fields =
                [
                    new AutoCastField { Name = "missing", Format = AutoCastFormats.Integer }
                ]
            }))
            .ThrowsExactly<IndexOutOfRangeException>()
            .WithMessage("Column 'missing' was not found.");
    }

    [Test]
    [DisplayName("Where после AutoCast работает с приведенными типами")]
    public async Task Where_after_auto_cast_uses_converted_values()
    {
        using var table = CreateTextTable("id", "amount");
        table.Rows.Add("1", "10.50");
        table.Rows.Add("2", "20.00");

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader
            .Normalize()
            .AutoCast(new AutoCastSchema
            {
                Fields =
                [
                    new AutoCastField { Name = "id", Format = AutoCastFormats.Integer },
                    new AutoCastField { Name = "amount", Format = AutoCastFormats.InvariantNumber }
                ]
            })
            .Where(row => row.Number("amount") > 15m);

        await Assert.That(reader).HaveData(
            columns: ["id", "amount"],
            types: [DataType.Integer, DataType.Number],
            rows: [
                (2L, 20.00m)
            ]);
    }

    private static DataTable CreateTextTable(params string[] columns)
    {
        var table = new DataTable();
        foreach (var column in columns)
        {
            table.Columns.Add(column, typeof(string));
        }

        return table;
    }
}

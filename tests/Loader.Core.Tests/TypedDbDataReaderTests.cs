using System.Data;
using Loader.Core.Data;
using Loader.Core.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Loader.Core.Tests;

public sealed class DomainDataReaderTests
{
    [Test]
    [DisplayName("DomainDataReader сводит string к Text и сохраняет origin DataTypeName")]
    public async Task Converts_supported_text_values_to_string()
    {
        using var table = new DataTable();
        table.Columns.Add("payload", typeof(string));
        table.Rows.Add("{\"name\":\"moscow\"}");

        using var rawReader = table.CreateDataReader();
        var originTypeName = rawReader.GetDataTypeName(0);
        using var reader = rawReader.Normalize();

        await Assert.That(reader.Read()).IsTrue();
        await Assert.That(reader.GetFieldType(0)).IsEqualTo(typeof(string));
        await Assert.That(reader.GetFieldType(reader.GetOrdinal("payload"))).IsEqualTo(typeof(string));
        await Assert.That(reader.GetDataTypeName(0)).IsEqualTo(originTypeName);
        await Assert.That(reader.DataSchema.Fields[0].DataType).IsEqualTo(DataType.Text);
        await Assert.That((string)reader.GetValue(0)).IsEqualTo("{\"name\":\"moscow\"}");
        await Assert.That((string)reader["payload"]).IsEqualTo("{\"name\":\"moscow\"}");
        await Assert.That(() => reader.GetOrdinal("PAYLOAD"))
            .ThrowsExactly<IndexOutOfRangeException>()
            .WithMessage("Column 'PAYLOAD' was not found.");
    }

    [Test]
    [DisplayName("DomainDataReader сводит базовые CLR-типы к доменным типам и значениям")]
    public async Task Converts_basic_clr_values_to_domain_values()
    {
        using var table = new DataTable();
        table.Columns.Add("text", typeof(string));
        table.Columns.Add("integer", typeof(int));
        table.Columns.Add("number", typeof(decimal));
        table.Columns.Add("boolean", typeof(bool));
        table.Columns.Add("datetime", typeof(DateTime));
        table.Columns.Add("time", typeof(TimeSpan));
        table.Rows.Add("Moscow", 42, 10.50m, true, new DateTime(2026, 1, 2, 3, 4, 5), new TimeSpan(6, 7, 8));

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["text", "integer", "number", "boolean", "datetime", "time"],
            types: [DataType.Text, DataType.Integer, DataType.Number, DataType.Boolean, DataType.DateTime, DataType.Time],
            rows: [
                ("Moscow", 42L, 10.50m, true, new DateTime(2026, 1, 2, 3, 4, 5), new TimeOnly(6, 7, 8))
            ]);
    }

    [Test]
    [DisplayName("DomainDataReader сводит null значение к DBNull")]
    public async Task Converts_null_values_to_db_null()
    {
        using var table = new DataTable();
        table.Columns.Add("name", typeof(string));
        table.Rows.Add(DBNull.Value);

        using var rawReader = table.CreateDataReader();
        using var reader = rawReader.Normalize();

        await Assert.That(reader.Read()).IsTrue();
        await Assert.That(reader.IsDBNull(0)).IsTrue();
        await Assert.That(reader.GetValue(0)).IsEqualTo(DBNull.Value);
        await Assert.That(reader["name"]).IsEqualTo(DBNull.Value);
    }

    [Test]
    [DisplayName("DomainDataReader GetValues копирует текущую строку и возвращает количество колонок")]
    public async Task Get_values_copies_current_row()
    {
        using var table = new DataTable();
        table.Columns.Add("id", typeof(int));
        table.Columns.Add("name", typeof(string));
        table.Rows.Add(1, "Moscow");

        using var rawReader = table.CreateDataReader();
        using var reader = rawReader.Normalize();
        var values = new object[4];

        await Assert.That(reader.Read()).IsTrue();
        var copied = reader.GetValues(values);

        await Assert.That(copied).IsEqualTo(2);
        await Assert.That(values[0]).IsEqualTo(1L);
        await Assert.That(values[1]).IsEqualTo("Moscow");
        await Assert.That(values[2]).IsNull();
        await Assert.That(values[3]).IsNull();
    }

    [Test]
    [DisplayName("DomainDataReader GetValue до Read кидает ошибку позиции reader")]
    public async Task Get_value_before_read_throws()
    {
        using var table = new DataTable();
        table.Columns.Add("id", typeof(int));
        table.Rows.Add(1);

        using var rawReader = table.CreateDataReader();
        using var reader = rawReader.Normalize();

        await Assert.That(() => reader.GetValue(0))
            .ThrowsExactly<InvalidOperationException>()
            .WithMessage("Reader is not positioned on a row.");
    }

    [Test]
    [DisplayName("DomainDataReader GetValue после конца потока кидает ошибку позиции reader")]
    public async Task Get_value_after_end_throws()
    {
        using var table = new DataTable();
        table.Columns.Add("id", typeof(int));
        table.Rows.Add(1);

        using var rawReader = table.CreateDataReader();
        using var reader = rawReader.Normalize();

        await Assert.That(reader.Read()).IsTrue();
        await Assert.That(reader.Read()).IsFalse();
        await Assert.That(() => reader.GetValue(0))
            .ThrowsExactly<InvalidOperationException>()
            .WithMessage("Reader is not positioned on a row.");
    }

    [Test]
    [DisplayName("DomainDataReader для неизвестного CLR-типа кидает ошибку маппинга")]
    public async Task Throws_for_unknown_clr_type()
    {
        using var table = new DataTable();
        table.Columns.Add("payload", typeof(object));

        using var rawReader = table.CreateDataReader();

        await Assert.That(() => rawReader.Normalize())
            .ThrowsExactly<NotSupportedException>()
            .WithMessage("CLR type 'System.Object' is not supported by Loader data type mapper.");
    }
}

using System.Data;
using System.Data.Common;
using Loader.Core.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Loader.Core.Tests;

public sealed class DomainDataReaderTests
{
    [Test]
    [DisplayName("Normalize поверх DomainDataReader возвращает тот же reader без повторной нормализации")]
    public async Task Normalize_over_domain_reader_returns_same_instance()
    {
        using var table = new DataTable();
        table.Columns.Add("id", typeof(int));
        table.Rows.Add(1);

        using var rawReader = table.CreateDataReader();
        using var reader = rawReader.Normalize();

        var normalizedAgain = reader.Normalize();

        await Assert.That(ReferenceEquals(reader, normalizedAgain)).IsTrue();
    }

    [Test]
    [DisplayName("Normalize после Where возвращает тот же Domain reader без повторной нормализации")]
    public async Task Normalize_after_where_returns_same_instance()
    {
        using var table = new DataTable();
        table.Columns.Add("id", typeof(int));
        table.Rows.Add(1);

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader
            .Normalize()
            .Where(row => row.Integer("id") == 1);

        var normalizedAgain = reader.Normalize();

        await Assert.That(ReferenceEquals(reader, normalizedAgain)).IsTrue();
    }

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
                ("Moscow", 42, 10.50m, true, new DateTime(2026, 1, 2, 3, 4, 5), new TimeOnly(6, 7, 8))
            ]);
    }

    [Test]
    [DisplayName("DomainDataReader typed getters читают нормализованные значения")]
    public async Task Typed_getters_read_normalized_values()
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

        await Assert.That(reader.Read()).IsTrue();
        await Assert.That(reader.GetString(0)).IsEqualTo("Moscow");
        await Assert.That(reader.GetInt32(1)).IsEqualTo(42);
        await Assert.That(reader.GetDecimal(2)).IsEqualTo(10.50m);
        await Assert.That(reader.GetBoolean(3)).IsTrue();
        await Assert.That(reader.GetDateTime(4)).IsEqualTo(new DateTime(2026, 1, 2, 3, 4, 5));
        await Assert.That(reader.GetFieldValue<TimeOnly>(5)).IsEqualTo(new TimeOnly(6, 7, 8));
    }

    [Test]
    [DisplayName("Normalize without buffer GetString uses inner GetString and does not call inner GetValue")]
    public async Task Normalize_without_buffer_get_string_uses_inner_typed_accessor()
    {
        using var table = new DataTable();
        table.Columns.Add("name", typeof(string));
        table.Rows.Add("Moscow");

        using var rawReader = new CountingTypedAccessReader(table.CreateDataReader());
        using var reader = rawReader.Normalize(new NormalizeOptions { Buffer = false });

        await Assert.That(reader.Read()).IsTrue();
        await Assert.That(reader.GetString(0)).IsEqualTo("Moscow");

        await Assert.That(rawReader.GetStringCalls).IsEqualTo(1);
        await Assert.That(rawReader.GetValueCalls).IsEqualTo(0);
    }

    [Test]
    [DisplayName("Normalize without buffer GetInt16 uses inner GetInt16 and does not call inner GetValue")]
    public async Task Normalize_without_buffer_get_int16_uses_inner_typed_accessor()
    {
        using var table = new DataTable();
        table.Columns.Add("id", typeof(short));
        table.Rows.Add((short)42);

        using var rawReader = new CountingTypedAccessReader(table.CreateDataReader());
        using var reader = rawReader.Normalize(new NormalizeOptions { Buffer = false });

        await Assert.That(reader.Read()).IsTrue();
        await Assert.That(reader.GetInt16(0)).IsEqualTo((short)42);

        await Assert.That(rawReader.GetInt16Calls).IsEqualTo(1);
        await Assert.That(rawReader.GetValueCalls).IsEqualTo(0);
    }

    [Test]
    [DisplayName("DomainDataReader typed getter кидает InvalidCastException если accessor не совпадает со схемой")]
    public async Task Typed_getter_not_matching_schema_clr_type_throws()
    {
        using var table = new DataTable();
        table.Columns.Add("id", typeof(int));
        table.Rows.Add(42);

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader.Normalize();

        await Assert.That(reader.Read()).IsTrue();
        await Assert.That(() => reader.GetInt64(0))
            .ThrowsExactly<InvalidCastException>()
            .WithMessage("Column 'id' at ordinal 0 has CLR type 'System.Int32' and cannot be read with accessor 'GetInt64'.");
        await Assert.That(() => reader.GetFieldValue<long>(0))
            .ThrowsExactly<InvalidCastException>()
            .WithMessage("Column 'id' at ordinal 0 has CLR type 'System.Int32' and cannot be read with accessor 'GetFieldValue<Int64>'.");
    }

    [Test]
    [DisplayName("DomainDataReader GetEnumerator запрещен чтобы не обходить accessors")]
    public async Task Get_enumerator_is_not_supported()
    {
        using var table = new DataTable();
        table.Columns.Add("id", typeof(int));
        table.Rows.Add(1);

        using var rawReader = table.CreateDataReader();
        using var reader = rawReader.Normalize();

        await Assert.That(() => reader.GetEnumerator())
            .ThrowsExactly<NotSupportedException>()
            .WithMessage("DomainDataReader supports only explicit Read() and accessor methods.");
    }

    [Test]
    [DisplayName("DomainDataReader GetColumnSchema возвращает нормализованную схему")]
    public async Task Get_column_schema_returns_normalized_schema()
    {
        using var table = new DataTable();
        table.Columns.Add("created", typeof(TimeSpan));
        table.Rows.Add(new TimeSpan(3, 4, 5));

        using var rawReader = table.CreateDataReader();
        using var reader = rawReader.Normalize();

        var column = reader.GetColumnSchema().Single();

        await Assert.That(column.ColumnName).IsEqualTo("created");
        await Assert.That(column.ColumnOrdinal).IsEqualTo(0);
        await Assert.That(column.DataType).IsEqualTo(typeof(TimeOnly));
        await Assert.That(reader.DataSchema.Fields[0].ClrType).IsEqualTo(typeof(TimeOnly));
    }

    [Test]
    [DisplayName("DomainDataReader GetSchemaTable возвращает нормализованную схему")]
    public async Task Get_schema_table_returns_normalized_schema()
    {
        using var table = new DataTable();
        table.Columns.Add("created", typeof(TimeSpan));
        table.Rows.Add(new TimeSpan(3, 4, 5));

        using var rawReader = table.CreateDataReader();
        using var reader = rawReader.Normalize();

        var schemaTable = reader.GetSchemaTable();

        await Assert.That((string)schemaTable.Rows[0][SchemaTableColumn.ColumnName]).IsEqualTo("created");
        await Assert.That((int)schemaTable.Rows[0][SchemaTableColumn.ColumnOrdinal]).IsEqualTo(0);
        await Assert.That((Type)schemaTable.Rows[0][SchemaTableColumn.DataType]).IsEqualTo(typeof(TimeOnly));
    }

    [Test]
    [DisplayName("Normalize сохраняет доступную ADO metadata из inner reader")]
    public async Task Normalize_preserves_available_inner_ado_metadata()
    {
        using var table = new DataTable();
        var name = table.Columns.Add("name", typeof(string));
        name.AllowDBNull = false;
        name.MaxLength = 12;
        table.Rows.Add("Moscow");

        using var rawReader = table.CreateDataReader();
        using var reader = rawReader.Normalize();

        var field = reader.DataSchema.Fields[0];
        var column = reader.GetColumnSchema().Single();
        var schemaTable = reader.GetSchemaTable();

        await Assert.That(field.AllowDBNull).IsNotNull();
        await Assert.That(field.AllowDBNull!.Value).IsFalse();
        await Assert.That(field.ColumnSize).IsEqualTo(12);
        await Assert.That(column.AllowDBNull).IsNotNull();
        await Assert.That(column.AllowDBNull!.Value).IsFalse();
        await Assert.That(column.ColumnSize).IsEqualTo(12);
        await Assert.That((bool)schemaTable.Rows[0][SchemaTableColumn.AllowDBNull]).IsFalse();
        await Assert.That((int)schemaTable.Rows[0][SchemaTableColumn.ColumnSize]).IsEqualTo(12);
    }

    [Test]
    [DisplayName("DomainDataReader pipeline GetEnumerator запрещен чтобы не обходить accessors")]
    public async Task Pipeline_get_enumerator_is_not_supported()
    {
        using var table = new DataTable();
        table.Columns.Add("id", typeof(int));
        table.Rows.Add(1);

        using var rawReader = table.CreateDataReader();
        using var reader = rawReader
            .Normalize()
            .Where(row => row.Integer("id") == 1)
            .Limit(1)
            .CollectMeta(new DataMetaContainer());

        await Assert.That(() => reader.GetEnumerator())
            .ThrowsExactly<NotSupportedException>()
            .WithMessage("DomainDataReader supports only explicit Read() and accessor methods.");
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
    [DisplayName("DomainDataReader typed getter на DBNull кидает InvalidCastException")]
    public async Task Typed_getter_over_dbnull_throws()
    {
        using var table = new DataTable();
        table.Columns.Add("name", typeof(string));
        table.Rows.Add(DBNull.Value);

        using var rawReader = table.CreateDataReader();
        using var reader = rawReader.Normalize();

        await Assert.That(reader.Read()).IsTrue();
        await Assert.That(() => reader.GetString(0))
            .ThrowsExactly<InvalidCastException>()
            .WithMessage("Column ordinal 0 contains DBNull.");
        await Assert.That(reader.GetFieldValue<object>(0)).IsEqualTo(DBNull.Value);
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
        await Assert.That(values[0]).IsEqualTo(1);
        await Assert.That(values[1]).IsEqualTo("Moscow");
        await Assert.That(values[2]).IsNull();
        await Assert.That(values[3]).IsNull();
    }

    [Test]
    [DisplayName("Normalize по умолчанию читает значения лениво без буфера")]
    public async Task Normalize_default_reads_values_lazily_without_buffer()
    {
        using var table = new DataTable();
        table.Columns.Add("id", typeof(int));
        table.Columns.Add("name", typeof(string));
        table.Rows.Add(1, "Moscow");

        using var rawReader = new CountingValueReader(table.CreateDataReader());
        using var reader = rawReader.Normalize();

        await Assert.That(reader.Read()).IsTrue();
        await Assert.That(rawReader.ValueReadAttempts).IsEqualTo(0);

        await Assert.That(reader.GetValue(0)).IsEqualTo(1);
        await Assert.That(rawReader.ValueReadAttempts).IsEqualTo(1);

        await Assert.That(reader.GetValue(1)).IsEqualTo("Moscow");
        await Assert.That(rawReader.ValueReadAttempts).IsEqualTo(2);
    }

    [Test]
    [DisplayName("Normalize с Buffer false читает значения лениво по запросу")]
    public async Task Normalize_without_buffer_reads_values_lazily()
    {
        using var table = new DataTable();
        table.Columns.Add("id", typeof(int));
        table.Columns.Add("name", typeof(string));
        table.Rows.Add(1, "Moscow");

        using var rawReader = new CountingValueReader(table.CreateDataReader());
        using var reader = rawReader.Normalize(new NormalizeOptions { Buffer = false });

        await Assert.That(reader.Read()).IsTrue();
        await Assert.That(rawReader.ValueReadAttempts).IsEqualTo(0);

        await Assert.That(reader.GetValue(0)).IsEqualTo(1);
        await Assert.That(rawReader.ValueReadAttempts).IsEqualTo(1);

        await Assert.That(reader.GetValue(0)).IsEqualTo(1);
        await Assert.That(rawReader.ValueReadAttempts).IsEqualTo(2);
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
    [DisplayName("DomainDataReader явно неподдержанный CLR-тип не читает из inner reader и возвращает DBNull")]
    public async Task Explicit_unsupported_clr_type_is_not_read_and_returns_dbnull()
    {
        using var table = new DataTable();
        table.Columns.Add("payload", typeof(byte[]));
        table.Rows.Add(new byte[] { 0xde, 0xad });

        using var rawReader = new ThrowOnValueReader(table.CreateDataReader());
        using var reader = rawReader.Normalize();

        await Assert.That(reader.Read()).IsTrue();
        await Assert.That(reader.GetValue(0)).IsEqualTo(DBNull.Value);
        await Assert.That(reader.IsDBNull(0)).IsTrue();
        await Assert.That(reader.GetFieldType(0)).IsEqualTo(typeof(DBNull));
        await Assert.That(rawReader.ValueReadAttempts).IsEqualTo(0);
    }

    [Test]
    [DisplayName("DomainDataReader для неизвестного CLR-типа кидает ошибку маппинга")]
    public async Task Throws_for_unknown_clr_type()
    {
        using var table = new DataTable();
        table.Columns.Add("payload", typeof(object));

        using var rawReader = table.CreateDataReader();

        await Assert.That(() => rawReader.Normalize())
            .ThrowsExactly<UnknownClrTypeException>()
            .WithMessage("CLR type 'System.Object' is unknown to Loader data type mapper.");
    }

    private sealed class CountingValueReader : DbDataReaderDecorator
    {
        public CountingValueReader(DbDataReader inner)
            : base(inner)
        {
        }

        public int ValueReadAttempts { get; private set; }

        public override object GetValue(int ordinal)
        {
            ValueReadAttempts++;
            return Inner.GetValue(ordinal);
        }
    }

    private sealed class ThrowOnValueReader : DbDataReaderDecorator
    {
        public ThrowOnValueReader(DbDataReader inner)
            : base(inner)
        {
        }

        public int ValueReadAttempts { get; private set; }

        public override bool IsDBNull(int ordinal)
        {
            ValueReadAttempts++;
            throw new InvalidOperationException("Unsupported value must not be checked.");
        }

        public override object GetValue(int ordinal)
        {
            ValueReadAttempts++;
            throw new InvalidOperationException("Unsupported value must not be read.");
        }
    }

    private sealed class CountingTypedAccessReader : DbDataReaderDecorator
    {
        public CountingTypedAccessReader(DbDataReader inner)
            : base(inner)
        {
        }

        public int GetStringCalls { get; private set; }

        public int GetInt16Calls { get; private set; }

        public int GetValueCalls { get; private set; }

        public override string GetString(int ordinal)
        {
            GetStringCalls++;
            return Inner.GetString(ordinal);
        }

        public override short GetInt16(int ordinal)
        {
            GetInt16Calls++;
            return Inner.GetInt16(ordinal);
        }

        public override object GetValue(int ordinal)
        {
            GetValueCalls++;
            return Inner.GetValue(ordinal);
        }
    }
}

using Loader.Core.Decorators;
using Loader.Core.Models;
using Loader.Core.Writers.ClickHouse;

namespace Loader.Core.Tests;

public sealed class ClickHouseColumnTypeResolverTests
{
    [Test]
    [MethodDataSource(nameof(IntegerClrTypeCases))]
    [DisplayName("ClickHouse type resolver Integer без meta выбирает тип по CLR")]
    public async Task Integer_without_meta_uses_clr_type(Type clrType, string expected)
    {
        var actual = Resolve(Field(DataType.Integer, clrType), meta: null);

        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    [MethodDataSource(nameof(IntegerBoundsCases))]
    [DisplayName("ClickHouse type resolver Integer с meta выбирает минимальный тип по min max")]
    public async Task Integer_with_meta_uses_minimal_type_by_bounds(decimal min, decimal max, string expected)
    {
        var actual = Resolve(Field(DataType.Integer, typeof(long)), Meta(DataType.Integer, min, max));

        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    [MethodDataSource(nameof(NullableCases))]
    [DisplayName("ClickHouse type resolver nullable определяется по meta density или schema AllowDBNull")]
    public async Task Nullable_is_resolved_from_meta_density_or_schema_flag(
        DataType dataType,
        Type clrType,
        bool allowDbNull,
        DataColumnMeta? meta,
        string expected)
    {
        var actual = Resolve(Field(dataType, clrType, allowDbNull: allowDbNull), meta);

        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    [MethodDataSource(nameof(NumberCases))]
    [DisplayName("ClickHouse type resolver Number выбирает float double decimal precision scale")]
    public async Task Number_uses_float_double_or_decimal_shape(Type clrType, int? precision, int? scale, string expected)
    {
        var field = Field(DataType.Number, clrType, precision: precision, scale: scale);

        var actual = Resolve(field, meta: null);

        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    [DisplayName("ClickHouse type resolver Number meta precision scale важнее schema precision scale")]
    public async Task Number_meta_decimal_shape_overrides_schema_decimal_shape()
    {
        var field = Field(DataType.Number, typeof(decimal), precision: 20, scale: 5);
        var meta = Meta(DataType.Number, 1.23m, 123.45m, decimalPrecision: 5, decimalScale: 2);

        var actual = Resolve(field, meta);

        await Assert.That(actual).IsEqualTo("Decimal(5, 2)");
    }

    [Test]
    [MethodDataSource(nameof(TextCases))]
    [DisplayName("ClickHouse type resolver Text учитывает low cardinality cardinality exceeded и nullable")]
    public async Task Text_uses_low_cardinality_cardinality_exceeded_and_nullable(
        bool useLowCardinality,
        DataColumnMeta? meta,
        string expected)
    {
        var actual = Resolve(
            Field(DataType.Text, typeof(string)),
            meta,
            new ClickHouseWriteOptions
            {
                TableName = new ClickHouseTableName { Table = "target" },
                UseLowCardinalityForText = useLowCardinality
            });

        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    [MethodDataSource(nameof(PrimitiveCases))]
    [DisplayName("ClickHouse type resolver остальные DataType получают стабильный CH тип")]
    public async Task Primitive_data_types_map_to_expected_clickhouse_types(DataType dataType, Type clrType, string expected)
    {
        var actual = Resolve(Field(dataType, clrType), meta: null);

        await Assert.That(actual).IsEqualTo(expected);
    }

    public static IEnumerable<(Type ClrType, string Expected)> IntegerClrTypeCases()
    {
        yield return (typeof(byte), "UInt8");
        yield return (typeof(sbyte), "Int8");
        yield return (typeof(short), "Int16");
        yield return (typeof(ushort), "UInt16");
        yield return (typeof(int), "Int32");
        yield return (typeof(uint), "UInt32");
        yield return (typeof(long), "Int64");
        yield return (typeof(ulong), "UInt64");
        yield return (typeof(object), "Int64");
    }

    public static IEnumerable<(decimal Min, decimal Max, string Expected)> IntegerBoundsCases()
    {
        yield return (0m, byte.MaxValue, "UInt8");
        yield return (0m, byte.MaxValue + 1m, "UInt16");
        yield return (0m, ushort.MaxValue + 1m, "UInt32");
        yield return (0m, uint.MaxValue + 1m, "UInt64");
        yield return (sbyte.MinValue, sbyte.MaxValue, "Int8");
        yield return (sbyte.MinValue - 1m, short.MaxValue, "Int16");
        yield return (short.MinValue - 1m, int.MaxValue, "Int32");
        yield return (int.MinValue - 1m, long.MaxValue, "Int64");
    }

    public static IEnumerable<(DataType DataType, Type ClrType, bool AllowDbNull, DataColumnMeta? Meta, string Expected)> NullableCases()
    {
        yield return (DataType.Integer, typeof(int), false, null, "Int32");
        yield return (DataType.Integer, typeof(int), true, null, "Nullable(Int32)");
        yield return (DataType.Integer, typeof(int), true, NotNullableMeta(DataType.Integer), "UInt8");
        yield return (DataType.Integer, typeof(int), false, NullableMeta(DataType.Integer), "Nullable(UInt8)");
        yield return (DataType.Text, typeof(string), false, NullableMeta(DataType.Text), "LowCardinality(Nullable(String))");
        yield return (DataType.Text, typeof(string), true, HighCardinalityMeta(DataType.Text), "String");
    }

    public static IEnumerable<(Type ClrType, int? Precision, int? Scale, string Expected)> NumberCases()
    {
        yield return (typeof(float), null, null, "Float32");
        yield return (typeof(double), null, null, "Float64");
        yield return (typeof(decimal), 9, 2, "Decimal(9, 2)");
        yield return (typeof(decimal), null, null, "Decimal(38, 10)");
        yield return (typeof(object), null, null, "Float64");
    }

    public static IEnumerable<(bool UseLowCardinality, DataColumnMeta? Meta, string Expected)> TextCases()
    {
        yield return (true, null, "String");
        yield return (false, LowCardinalityMeta(), "String");
        yield return (true, LowCardinalityMeta(), "LowCardinality(String)");
        yield return (true, NullableMeta(DataType.Text), "LowCardinality(Nullable(String))");
        yield return (true, HighCardinalityMeta(DataType.Text), "String");
    }

    public static IEnumerable<(DataType DataType, Type ClrType, string Expected)> PrimitiveCases()
    {
        yield return (DataType.DateTime, typeof(DateTime), "DateTime64(3)");
        yield return (DataType.Date, typeof(DateOnly), "Date");
        yield return (DataType.Time, typeof(TimeOnly), "String");
        yield return (DataType.Boolean, typeof(bool), "Bool");
    }

    private static string Resolve(
        DataField field,
        DataColumnMeta? meta,
        ClickHouseWriteOptions? options = null)
    {
        var resolver = new ClickHouseColumnTypeResolver(options ?? new ClickHouseWriteOptions
        {
            TableName = new ClickHouseTableName
            {
                Table = "target"
            }
        });

        return resolver.Resolve(field, meta);
    }

    private static DataField Field(
        DataType dataType,
        Type clrType,
        bool allowDbNull = false,
        int? precision = null,
        int? scale = null)
    {
        return new DataField
        {
            Ordinal = 0,
            Name = "value",
            DataType = dataType,
            ClrType = clrType,
            AllowDBNull = allowDbNull,
            NumericPrecision = precision,
            NumericScale = scale,
            Convert = null,
            ReadValue = true
        };
    }

    private static DataColumnMeta Meta(
        DataType dataType,
        decimal min,
        decimal max,
        int? decimalPrecision = null,
        int? decimalScale = null)
    {
        var meta = new DataColumnMeta(0, "value", dataType, decimalPrecision, decimalScale, maxCardinality: 20);
        meta.CollectValue(min, rowCount: 1);
        meta.CollectValue(max, rowCount: 2);
        return meta;
    }

    private static DataColumnMeta NotNullableMeta(DataType dataType)
    {
        var meta = new DataColumnMeta(0, "value", dataType, decimalPrecision: null, decimalScale: null, maxCardinality: 20);
        meta.CollectValue(1, rowCount: 1);
        return meta;
    }

    private static DataColumnMeta NullableMeta(DataType dataType)
    {
        var meta = new DataColumnMeta(0, "value", dataType, decimalPrecision: null, decimalScale: null, maxCardinality: 20);
        meta.CollectValue(DBNull.Value, rowCount: 1);
        meta.CollectValue(dataType == DataType.Text ? "x" : 1, rowCount: 2);
        return meta;
    }

    private static DataColumnMeta LowCardinalityMeta()
    {
        var meta = new DataColumnMeta(0, "value", DataType.Text, decimalPrecision: null, decimalScale: null, maxCardinality: 20);
        meta.CollectValue("Moscow", rowCount: 1);
        meta.CollectValue("London", rowCount: 2);
        return meta;
    }

    private static DataColumnMeta HighCardinalityMeta(DataType dataType)
    {
        var meta = new DataColumnMeta(0, "value", dataType, decimalPrecision: null, decimalScale: null, maxCardinality: 1);
        meta.CollectValue("Moscow", rowCount: 1);
        meta.CollectValue("London", rowCount: 2);
        return meta;
    }
}

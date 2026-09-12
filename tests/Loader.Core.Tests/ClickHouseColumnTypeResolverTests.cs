using System.Numerics;
using ClickHouse.Client.Numerics;
using Loader.Core.Models;
using Loader.Core.Writers.ClickHouse;

namespace Loader.Core.Tests;

public sealed class ClickHouseColumnTypeResolverTests
{
    [Test]
    [MethodDataSource(nameof(IntegerClrTypeCases))]
    [DisplayName("ClickHouse type resolver Integer выбирает тип по CLR")]
    public async Task Integer_uses_clr_type(Type clrType, string expected)
    {
        var actual = Resolve(Field(DataType.Integer, clrType));

        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    [MethodDataSource(nameof(NullableCases))]
    [DisplayName("ClickHouse type resolver nullable определяется по schema AllowDBNull")]
    public async Task Nullable_is_resolved_from_schema_flag(
        DataType dataType,
        Type clrType,
        bool? allowDbNull,
        string expected)
    {
        var actual = Resolve(Field(dataType, clrType, allowDbNull: allowDbNull));

        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    [MethodDataSource(nameof(NumberCases))]
    [DisplayName("ClickHouse type resolver Number выбирает float double decimal precision scale")]
    public async Task Number_uses_float_double_or_decimal_shape(Type clrType, int? precision, int? scale, string expected)
    {
        var field = Field(DataType.Number, clrType, precision: precision, scale: scale);

        var actual = Resolve(field);

        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    [MethodDataSource(nameof(PrimitiveCases))]
    [DisplayName("ClickHouse type resolver остальные DataType получают стабильный CH тип")]
    public async Task Primitive_data_types_map_to_expected_clickhouse_types(DataType dataType, Type clrType, string expected)
    {
        var actual = Resolve(Field(dataType, clrType));

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
        yield return (typeof(BigInteger), "Int256");
        yield return (typeof(object), "Int64");
    }

    public static IEnumerable<(DataType DataType, Type ClrType, bool? AllowDbNull, string Expected)> NullableCases()
    {
        yield return (DataType.Integer, typeof(int), false, "Int32");
        yield return (DataType.Integer, typeof(int), true, "Nullable(Int32)");
        yield return (DataType.Integer, typeof(int), null, "Nullable(Int32)");
        yield return (DataType.Text, typeof(string), false, "String");
        yield return (DataType.Text, typeof(string), true, "Nullable(String)");
    }

    public static IEnumerable<(Type ClrType, int? Precision, int? Scale, string Expected)> NumberCases()
    {
        yield return (typeof(float), null, null, "Float32");
        yield return (typeof(double), null, null, "Float64");
        yield return (typeof(decimal), 9, 2, "Decimal(9, 2)");
        yield return (typeof(decimal), 10, 0, "Decimal(10, 0)");
        yield return (typeof(decimal), 0, 0, "Decimal(38, 10)");
        yield return (typeof(decimal), null, null, "Decimal(38, 10)");
        yield return (typeof(ClickHouseDecimal), 20, 4, "Decimal(20, 4)");
        yield return (typeof(ClickHouseDecimal), null, null, "Decimal(38, 10)");
        yield return (typeof(object), null, null, "Float64");
    }

    public static IEnumerable<(DataType DataType, Type ClrType, string Expected)> PrimitiveCases()
    {
        yield return (DataType.DateTime, typeof(DateTime), "DateTime64(3)");
        yield return (DataType.Date, typeof(DateOnly), "Date");
        yield return (DataType.Time, typeof(TimeOnly), "DateTime");
        yield return (DataType.Boolean, typeof(bool), "Bool");
    }

    private static string Resolve(DataField field)
    {
        var resolver = new ClickHouseColumnTypeResolver(new ClickHouseWriteOptions
        {
            TableName = new ClickHouseTableName
            {
                Table = "target"
            }
        });

        return resolver.Resolve(field);
    }

    private static DataField Field(
        DataType dataType,
        Type clrType,
        bool? allowDbNull = false,
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
}

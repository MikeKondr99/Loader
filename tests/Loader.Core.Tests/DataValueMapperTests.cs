using System.Collections;
using System.Net;
using System.Net.NetworkInformation;
using System.Numerics;
using ClickHouse.Client.Numerics;
using Loader.Core.Data;
using NpgsqlTypes;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Loader.Core.Tests;

public sealed class DataValueMapperTests
{
    [Test]
    [MethodDataSource(nameof(ValueCases))]
    [DisplayName("DataValueMapper значение поддержанного CLR-типа мапит в ожидаемое значение")]
    public async Task Maps_value_to_expected_value(object input, object expected)
    {
        var mapping = DataValueMapper.Map(input);
        var actual = DataValueMapper.ConvertValue(mapping, input);

        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    [MethodDataSource(nameof(SupportedTypes))]
    [DisplayName("DataValueMapper для поддержанного CLR-типа возвращает CanMap true")]
    public async Task Supported_type_returns_true(Type type)
    {
        await Assert.That(DataValueMapper.CanMap(type)).IsTrue();
    }

    [Test]
    [MethodDataSource(nameof(ClrTypeCases))]
    [DisplayName("DataValueMapper для CLR-типа возвращает ожидаемый runtime CLR type")]
    public async Task Returns_expected_clr_type(Type sourceType, Type expectedClrType)
    {
        var mapping = DataValueMapper.MapType(sourceType);

        await Assert.That(mapping.ClrType).IsEqualTo(expectedClrType);
    }

    [Test]
    [MethodDataSource(nameof(SimpleTypesThatDoNotRequireConversion))]
    [DisplayName("DataValueMapper для простого CLR-типа не требует conversion")]
    public async Task Simple_type_does_not_require_conversion(Type sourceType)
    {
        var mapping = DataValueMapper.MapType(sourceType);

        await Assert.That(mapping.RequiresConversion).IsFalse();
    }

    [Test]
    [DisplayName("DataValueMapper неизвестный CLR-тип кидает UnknownClrTypeException")]
    public async Task Unknown_clr_type_throws()
    {
        await Assert.That(() => DataValueMapper.MapType(typeof(object)))
            .ThrowsExactly<UnknownClrTypeException>()
            .WithMessage("CLR type 'System.Object' is unknown to Loader data type mapper.");
    }

    [Test]
    [MethodDataSource(nameof(ExplicitUnsupportedTypes))]
    [DisplayName("DataValueMapper явно неподдержанный CLR-тип знает, но помечает как нечитаемый")]
    public async Task Explicit_unsupported_clr_type_is_known_but_not_readable(Type sourceType, DataType expectedType)
    {
        var mapping = DataValueMapper.MapType(sourceType);

        await Assert.That(DataValueMapper.CanMap(sourceType)).IsTrue();
        await Assert.That(mapping.DataType).IsEqualTo(expectedType);
        await Assert.That(mapping.ReadValue).IsFalse();
        await Assert.That(mapping.ClrType).IsEqualTo(typeof(DBNull));
    }

    public static IEnumerable<(object Input, object Expected)> ValueCases()
    {
        yield return ("text", "text");
        yield return (true, true);
        yield return ((byte)1, (byte)1);
        yield return ((sbyte)-1, (sbyte)-1);
        yield return ((short)2, (short)2);
        yield return ((ushort)3, (ushort)3);
        yield return (4, 4);
        yield return (5u, 5u);
        yield return (6L, 6L);
        yield return (7ul, 7ul);
        yield return (1.5f, 1.5f);
        yield return (2.5d, 2.5d);
        yield return (3.5m, 3.5m);
        yield return ((ClickHouseDecimal)12.34m, 12.34m);
        yield return (new DateTime(2026, 1, 2, 3, 4, 5), new DateTime(2026, 1, 2, 3, 4, 5));
        yield return (new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 2));
        yield return (new TimeOnly(3, 4, 5), new TimeOnly(3, 4, 5));
        yield return (new TimeSpan(3, 4, 5), new TimeOnly(3, 4, 5));
        yield return ('x', "x");
        yield return (Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        yield return (new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero), "2026-01-02T03:04:05.0000000+00:00");
        yield return (new BitArray([true, false, true]), "101");
        yield return (IPAddress.Parse("192.168.1.1"), "192.168.1.1");
        yield return (IPNetwork.Parse("192.168.0.0/24"), "192.168.0.0/24");
        yield return (PhysicalAddress.Parse("08002B010203"), "08:00:2b:01:02:03");
        yield return (new[] { 1, 2, 3 }, "{1,2,3}");
        yield return (new NpgsqlInet(IPAddress.Parse("192.168.1.1")), "192.168.1.1");
        yield return (new NpgsqlPoint(1, 2), "(1,2)");
        yield return (new NpgsqlLine(1, 2, 3), "{1,2,3}");
        yield return (new NpgsqlCircle(0, 0, 1), "<(0,0),1>");
        yield return (NpgsqlLogSequenceNumber.Parse("16/B374D848"), "16/B374D848");
        yield return (new NpgsqlTid(1, 2), "(1,2)");
        yield return (new NpgsqlRange<int>(1, true, false, 3, false, false), "[1,3)");
    }

    public static IEnumerable<Type> SupportedTypes()
    {
        foreach (var (sourceType, _) in ClrTypeCases())
        {
            yield return sourceType;
        }
    }

    public static IEnumerable<(Type SourceType, Type ExpectedClrType)> ClrTypeCases()
    {
        yield return (typeof(string), typeof(string));
        yield return (typeof(bool), typeof(bool));
        yield return (typeof(byte), typeof(byte));
        yield return (typeof(sbyte), typeof(sbyte));
        yield return (typeof(short), typeof(short));
        yield return (typeof(ushort), typeof(ushort));
        yield return (typeof(int), typeof(int));
        yield return (typeof(uint), typeof(uint));
        yield return (typeof(long), typeof(long));
        yield return (typeof(ulong), typeof(ulong));
        yield return (typeof(float), typeof(float));
        yield return (typeof(double), typeof(double));
        yield return (typeof(decimal), typeof(decimal));
        yield return (typeof(ClickHouseDecimal), typeof(decimal));
        yield return (typeof(BigInteger), typeof(DBNull));
        yield return (typeof(DateTime), typeof(DateTime));
        yield return (typeof(DateOnly), typeof(DateOnly));
        yield return (typeof(TimeOnly), typeof(TimeOnly));
        yield return (typeof(TimeSpan), typeof(TimeOnly));
        yield return (typeof(DBNull), typeof(DBNull));
        yield return (typeof(char), typeof(string));
        yield return (typeof(Guid), typeof(string));
        yield return (typeof(DateTimeOffset), typeof(string));
        yield return (typeof(byte[]), typeof(DBNull));
        yield return (typeof(Array), typeof(string));
        yield return (typeof(BitArray), typeof(string));
        yield return (typeof(IPAddress), typeof(string));
        yield return (typeof(IPNetwork), typeof(string));
        yield return (typeof(PhysicalAddress), typeof(string));
        yield return (typeof(int[]), typeof(string));
        yield return (typeof(NpgsqlRange<int>), typeof(string));
        yield return (typeof(Tuple<byte, string>), typeof(DBNull));
        yield return (typeof(Dictionary<string, byte>), typeof(DBNull));
        yield return (typeof(NpgsqlInet), typeof(string));
        yield return (typeof(NpgsqlPoint), typeof(string));
        yield return (typeof(NpgsqlLine), typeof(string));
        yield return (typeof(NpgsqlLSeg), typeof(string));
        yield return (typeof(NpgsqlBox), typeof(string));
        yield return (typeof(NpgsqlPath), typeof(string));
        yield return (typeof(NpgsqlPolygon), typeof(string));
        yield return (typeof(NpgsqlCircle), typeof(string));
        yield return (typeof(NpgsqlTsQuery), typeof(string));
        yield return (typeof(NpgsqlTsVector), typeof(string));
        yield return (typeof(NpgsqlLogSequenceNumber), typeof(string));
        yield return (typeof(NpgsqlTid), typeof(string));
    }

    public static IEnumerable<Type> SimpleTypesThatDoNotRequireConversion()
    {
        yield return typeof(string);
        yield return typeof(bool);
        yield return typeof(byte);
        yield return typeof(sbyte);
        yield return typeof(short);
        yield return typeof(ushort);
        yield return typeof(int);
        yield return typeof(uint);
        yield return typeof(long);
        yield return typeof(ulong);
        yield return typeof(float);
        yield return typeof(double);
        yield return typeof(decimal);
        yield return typeof(DateTime);
        yield return typeof(DateOnly);
        yield return typeof(TimeOnly);
    }

    public static IEnumerable<(Type SourceType, DataType ExpectedType)> ExplicitUnsupportedTypes()
    {
        yield return (typeof(byte[]), DataType.Text);
        yield return (typeof(DBNull), DataType.Text);
        yield return (typeof(BigInteger), DataType.Integer);
        yield return (typeof(Tuple<byte, string>), DataType.Text);
        yield return (typeof(Dictionary<string, byte>), DataType.Text);
    }
}

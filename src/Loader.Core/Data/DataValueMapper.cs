using System.Collections;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using ClickHouse.Client.Numerics;
using NpgsqlTypes;

namespace Loader.Core.Data;

/// <summary>
/// Единый mapper CLR-типов в доменные типы Loader.
/// Простые CLR-типы только классифицируются; conversion вызывается только там, где она нужна.
/// </summary>
public static class DataValueMapper
{
    private static readonly IReadOnlyDictionary<Type, DataValueMapping> Exact = new Dictionary<Type, DataValueMapping>
    {
        [typeof(string)] = Same(DataType.Text, typeof(string)),
        [typeof(bool)] = Same(DataType.Boolean, typeof(bool)),

        [typeof(byte)] = Same(DataType.Integer, typeof(byte)),
        [typeof(sbyte)] = Same(DataType.Integer, typeof(sbyte)),
        [typeof(short)] = Same(DataType.Integer, typeof(short)),
        [typeof(ushort)] = Same(DataType.Integer, typeof(ushort)),
        [typeof(int)] = Same(DataType.Integer, typeof(int)),
        [typeof(uint)] = Same(DataType.Integer, typeof(uint)),
        [typeof(long)] = Same(DataType.Integer, typeof(long)),
        [typeof(ulong)] = Same(DataType.Integer, typeof(ulong)),

        [typeof(float)] = Same(DataType.Number, typeof(float)),
        [typeof(double)] = Same(DataType.Number, typeof(double)),
        [typeof(decimal)] = Same(DataType.Number, typeof(decimal)),

        // ClickHouse Decimal(18, 2) 12.34 -> 12.34m
        [typeof(ClickHouseDecimal)] = ConvertTo(DataType.Number, typeof(decimal), static value => ((ClickHouseDecimal)value).ToDecimal(CultureInfo.InvariantCulture)),

        [typeof(DateTime)] = Same(DataType.DateTime, typeof(DateTime)),
        [typeof(DateOnly)] = Same(DataType.Date, typeof(DateOnly)),
        [typeof(TimeOnly)] = Same(DataType.Time, typeof(TimeOnly)),

        [typeof(TimeSpan)] = ConvertTo(DataType.Time, typeof(TimeOnly), static value => TimeOnly.FromTimeSpan((TimeSpan)value)),

        // 'x' -> "x"
        [typeof(char)] = ConvertTo(DataType.Text, typeof(string), static value => value.ToString()!),

        // aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa -> "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"
        [typeof(Guid)] = ConvertTo(DataType.Text, typeof(string), static value => ((Guid)value).ToString()),

        // 2026-01-02T03:04:05.0000000+00:00
        [typeof(DateTimeOffset)] = ConvertTo(DataType.Text, typeof(string), static value => ((DateTimeOffset)value).ToString("O", CultureInfo.InvariantCulture)),

        // bytea-like value -> "\xdeadbeef"
        [typeof(byte[])] = ConvertTo(DataType.Text, typeof(string), static value => "\\x" + Convert.ToHexString((byte[])value).ToLowerInvariant()),

        // fallback System.Array value -> "{1,2,3}"
        [typeof(Array)] = ConvertTo(DataType.Text, typeof(string), static value => ConvertArray((Array)value)),

        // BitArray true,false,true -> "101"
        [typeof(BitArray)] = ConvertTo(DataType.Text, typeof(string), static value => ConvertBitArray((BitArray)value)),

        // IPAddress 192.168.1.1 -> "192.168.1.1"
        [typeof(IPAddress)] = ConvertTo(DataType.Text, typeof(string), static value => ((IPAddress)value).ToString()),

        // IPNetwork 192.168.0.0/24 -> "192.168.0.0/24"
        [typeof(IPNetwork)] = ConvertTo(DataType.Text, typeof(string), static value => ((IPNetwork)value).ToString()),

        // PhysicalAddress bytes -> "08:00:2b:01:02:03"
        [typeof(PhysicalAddress)] = ConvertTo(DataType.Text, typeof(string), static value => ConvertPhysicalAddress((PhysicalAddress)value)),

        // NpgsqlInet 192.168.1.1 -> "192.168.1.1"
        [typeof(NpgsqlInet)] = ConvertTo(DataType.Text, typeof(string), static value => value.ToString()!),

        // NpgsqlPoint (1,2) -> "(1,2)"
        [typeof(NpgsqlPoint)] = ConvertTo(DataType.Text, typeof(string), static value => value.ToString()!),

        // NpgsqlLine -> "{A,B,C}" provider string
        [typeof(NpgsqlLine)] = ConvertTo(DataType.Text, typeof(string), static value => value.ToString()!),

        // NpgsqlLSeg [(0,0),(1,1)] -> "[(0,0),(1,1)]"
        [typeof(NpgsqlLSeg)] = ConvertTo(DataType.Text, typeof(string), static value => value.ToString()!),

        // NpgsqlBox ((0,0),(1,1)) -> "(1,1),(0,0)"
        [typeof(NpgsqlBox)] = ConvertTo(DataType.Text, typeof(string), static value => value.ToString()!),

        // NpgsqlPath -> "((0,0),(1,1),(2,0))"
        [typeof(NpgsqlPath)] = ConvertTo(DataType.Text, typeof(string), static value => value.ToString()!),

        // NpgsqlPolygon -> "((0,0),(1,0),(1,1),(0,1))"
        [typeof(NpgsqlPolygon)] = ConvertTo(DataType.Text, typeof(string), static value => value.ToString()!),

        // NpgsqlCircle -> "<(0,0),1>"
        [typeof(NpgsqlCircle)] = ConvertTo(DataType.Text, typeof(string), static value => value.ToString()!),

        // NpgsqlTsQuery -> "'hello' & 'world'"
        [typeof(NpgsqlTsQuery)] = ConvertTo(DataType.Text, typeof(string), static value => value.ToString()!),

        // NpgsqlTsVector -> "'hello':1 'world':2"
        [typeof(NpgsqlTsVector)] = ConvertTo(DataType.Text, typeof(string), static value => value.ToString()!),

        // NpgsqlLogSequenceNumber -> "16/B374D848"
        [typeof(NpgsqlLogSequenceNumber)] = ConvertTo(DataType.Text, typeof(string), static value => value.ToString()!),

        // NpgsqlTid -> "(block,offset)" provider string
        [typeof(NpgsqlTid)] = ConvertTo(DataType.Text, typeof(string), static value => value.ToString()!)
    };

    public static bool CanMap(object? input)
    {
        return input switch
        {
            null => false,
            Type type => CanMapType(type),
            object value => CanMapType(value.GetType())
        };
    }

    public static DataValueMapping Map(object input)
    {
        return input switch
        {
            Type type => MapType(type),
            object value => MapType(value.GetType())
        };
    }

    public static bool CanMapType(Type clrType)
    {
        var type = Nullable.GetUnderlyingType(clrType) ?? clrType;
        return Exact.ContainsKey(type) || IsNpgsqlCidr(type) || IsArrayType(type) || IsRangeType(type);
    }

    public static DataValueMapping MapType(Type clrType)
    {
        var type = Nullable.GetUnderlyingType(clrType) ?? clrType;

        if (Exact.TryGetValue(type, out var mapping))
        {
            return mapping;
        }

        return type switch
        {
            // NpgsqlCidr 192.168.0.0/24 -> "192.168.0.0/24"
            _ when IsNpgsqlCidr(type) => ConvertTo(DataType.Text, typeof(string), static value => value.ToString()!),

            // int[] {1,2,3} -> "{1,2,3}"
            _ when IsArrayType(type) => ConvertTo(DataType.Text, typeof(string), static value => ConvertArray((Array)value)),

            // int4range(1,3) -> "[1,3)"
            _ when IsRangeType(type) => ConvertTo(DataType.Text, typeof(string), ConvertRangeValue),

            _ => throw new NotSupportedException($"CLR type '{type.FullName}' is not supported by Loader data type mapper.")
        };
    }

    public static object ConvertValue(DataValueMapping mapping, object value)
    {
        return mapping.Convert is null ? value : mapping.Convert(value);
    }

    public static Type DefaultClrType(DataType dataType)
    {
        return dataType switch
        {
            DataType.Text => typeof(string),
            DataType.Integer => typeof(int),
            DataType.Number => typeof(decimal),
            DataType.DateTime => typeof(DateTime),
            DataType.Date => typeof(DateOnly),
            DataType.Time => typeof(TimeOnly),
            DataType.Boolean => typeof(bool),
            _ => throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null)
        };
    }

    private static DataValueMapping Same(DataType dataType, Type clrType)
    {
        return new DataValueMapping
        {
            DataType = dataType,
            ClrType = clrType,
            Convert = null
        };
    }

    private static DataValueMapping ConvertTo(DataType dataType, Type clrType, Func<object, object> convert)
    {
        return new DataValueMapping
        {
            DataType = dataType,
            ClrType = clrType,
            Convert = convert
        };
    }

    private static bool IsArrayType(Type type)
    {
        return type.IsArray && type != typeof(byte[]);
    }

    private static bool IsRangeType(Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(NpgsqlRange<>);
    }

    private static bool IsNpgsqlCidr(Type type)
    {
        return type.FullName == "NpgsqlTypes.NpgsqlCidr";
    }

    private static string ConvertRangeValue(object value)
    {
        var type = value.GetType();
        var isEmpty = (bool)type.GetProperty(nameof(NpgsqlRange<int>.IsEmpty))!.GetValue(value)!;
        if (isEmpty)
        {
            return "empty";
        }

        var lowerBoundInfinite = (bool)type.GetProperty(nameof(NpgsqlRange<int>.LowerBoundInfinite))!.GetValue(value)!;
        var upperBoundInfinite = (bool)type.GetProperty(nameof(NpgsqlRange<int>.UpperBoundInfinite))!.GetValue(value)!;
        var lowerBoundIsInclusive = (bool)type.GetProperty(nameof(NpgsqlRange<int>.LowerBoundIsInclusive))!.GetValue(value)!;
        var upperBoundIsInclusive = (bool)type.GetProperty(nameof(NpgsqlRange<int>.UpperBoundIsInclusive))!.GetValue(value)!;
        var lowerBound = lowerBoundInfinite ? string.Empty : ConvertRangeBound(type.GetProperty(nameof(NpgsqlRange<int>.LowerBound))!.GetValue(value));
        var upperBound = upperBoundInfinite ? string.Empty : ConvertRangeBound(type.GetProperty(nameof(NpgsqlRange<int>.UpperBound))!.GetValue(value));

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{(lowerBoundIsInclusive ? '[' : '(')}{lowerBound},{upperBound}{(upperBoundIsInclusive ? ']' : ')')}");
    }

    private static string ConvertRangeBound(object? value)
    {
        return value switch
        {
            null => string.Empty,
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            TimeOnly time => time.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            TimeSpan timeSpan => timeSpan.ToString("c", CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    private static string ConvertBitArray(BitArray bits)
    {
        var chars = new char[bits.Count];
        for (var i = 0; i < bits.Count; i++)
        {
            chars[i] = bits[i] ? '1' : '0';
        }

        return new string(chars);
    }

    private static string ConvertPhysicalAddress(PhysicalAddress address)
    {
        return string.Join(
            ":",
            address.GetAddressBytes().Select(static value => value.ToString("x2", CultureInfo.InvariantCulture)));
    }

    private static string ConvertArray(Array array)
    {
        var values = array.Cast<object?>().Select(ConvertArrayElement);
        return "{" + string.Join(",", values) + "}";
    }

    private static string ConvertArrayElement(object? value)
    {
        return value switch
        {
            null => "NULL",
            DBNull => "NULL",
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            DateOnly date => date.ToString("O", CultureInfo.InvariantCulture),
            TimeOnly time => time.ToString("O", CultureInfo.InvariantCulture),
            TimeSpan timeSpan => timeSpan.ToString("c", CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }
}

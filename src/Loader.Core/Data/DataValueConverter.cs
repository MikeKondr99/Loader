using System.Collections;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using NpgsqlTypes;

namespace Loader.Core.Data;

/// <summary>
/// Единая точка выбора и выполнения конверсии из провайдерских CLR-типов в канонические значения Loader.
/// </summary>
public static class DataValueConverter
{
    private static readonly DataValueConversion Text = new()
    {
        DataType = DataType.Text,
        CanonicalClrType = typeof(string),
        Convert = ConvertTextValue
    };

    private static readonly DataValueConversion Integer = new()
    {
        DataType = DataType.Integer,
        CanonicalClrType = typeof(long),
        Convert = static value => Convert.ToInt64(value, CultureInfo.InvariantCulture)
    };

    private static readonly DataValueConversion Number = new()
    {
        DataType = DataType.Number,
        CanonicalClrType = typeof(decimal),
        Convert = static value => Convert.ToDecimal(value, CultureInfo.InvariantCulture)
    };

    private static readonly DataValueConversion DateTime = new()
    {
        DataType = DataType.DateTime,
        CanonicalClrType = typeof(DateTime),
        Convert = static value => value is DateTime dateTime ? dateTime : Convert.ToDateTime(value, CultureInfo.InvariantCulture)
    };

    private static readonly DataValueConversion Date = new()
    {
        DataType = DataType.Date,
        CanonicalClrType = typeof(DateOnly),
        Convert = static value => value is DateOnly date ? date : DateOnly.FromDateTime(Convert.ToDateTime(value, CultureInfo.InvariantCulture))
    };

    private static readonly DataValueConversion Time = new()
    {
        DataType = DataType.Time,
        CanonicalClrType = typeof(TimeOnly),
        Convert = static value => value is TimeOnly time
            ? time
            : TimeOnly.FromTimeSpan(value is TimeSpan timeSpan ? timeSpan : TimeSpan.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture))
    };

    private static readonly DataValueConversion Boolean = new()
    {
        DataType = DataType.Boolean,
        CanonicalClrType = typeof(bool),
        Convert = static value => Convert.ToBoolean(value, CultureInfo.InvariantCulture)
    };

    public static DataValueConversion FromClrType(Type clrType)
    {
        var type = Nullable.GetUnderlyingType(clrType) ?? clrType;

        return type switch
        {
            _ when type == typeof(string) || type == typeof(char) || type == typeof(Guid) => Text,
            _ when type == typeof(DateTimeOffset) || type == typeof(byte[]) || type == typeof(Array) => Text,
            _ when type == typeof(BitArray) || type == typeof(IPAddress) || type == typeof(IPNetwork) || type == typeof(PhysicalAddress) => Text,
            _ when IsNpgsqlTextType(type) || IsArrayType(type) || IsRangeType(type) => Text,
            _ when type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort) => Integer,
            _ when type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong) => Integer,
            _ when type == typeof(float) || type == typeof(double) || type == typeof(decimal) => Number,
            _ when type == typeof(DateOnly) => Date,
            _ when type == typeof(TimeOnly) || type == typeof(TimeSpan) => Time,
            _ when type == typeof(DateTime) => DateTime,
            _ when type == typeof(bool) => Boolean,
            _ => throw new NotSupportedException($"CLR type '{type.FullName}' is not supported by Loader data type mapper.")
        };
    }

    public static DataValueConversion FromDataType(DataType dataType)
    {
        return dataType switch
        {
            DataType.Text => Text,
            DataType.Integer => Integer,
            DataType.Number => Number,
            DataType.DateTime => DateTime,
            DataType.Date => Date,
            DataType.Time => Time,
            DataType.Boolean => Boolean,
            _ => throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null)
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

    private static bool IsNpgsqlTextType(Type type)
    {
        return type.FullName == "NpgsqlTypes.NpgsqlCidr" ||
            type == typeof(NpgsqlInet) ||
            type == typeof(NpgsqlPoint) ||
            type == typeof(NpgsqlLine) ||
            type == typeof(NpgsqlLSeg) ||
            type == typeof(NpgsqlBox) ||
            type == typeof(NpgsqlPath) ||
            type == typeof(NpgsqlPolygon) ||
            type == typeof(NpgsqlCircle) ||
            type == typeof(NpgsqlTsQuery) ||
            type == typeof(NpgsqlTsVector) ||
            type == typeof(NpgsqlLogSequenceNumber) ||
            type == typeof(NpgsqlTid);
    }

    private static string ConvertTextValue(object value)
    {
        if (IsRangeValue(value))
        {
            return ConvertRangeValue(value);
        }

        return value switch
        {
            string text => text,
            char ch => ch.ToString(),
            Guid guid => guid.ToString(),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
            byte[] bytes => "\\x" + Convert.ToHexString(bytes).ToLowerInvariant(),
            BitArray bits => ConvertBitArray(bits),
            PhysicalAddress address => ConvertPhysicalAddress(address),
            Array array => ConvertArray(array),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    private static bool IsRangeValue(object value)
    {
        var type = value.GetType();
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(NpgsqlRange<>);
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
            _ => ConvertTextValue(value)
        };
    }
}

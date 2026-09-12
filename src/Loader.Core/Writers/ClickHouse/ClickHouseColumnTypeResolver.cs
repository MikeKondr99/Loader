using System.Numerics;
using ClickHouse.Client.Numerics;
using Loader.Core.Models;

namespace Loader.Core.Writers.ClickHouse;

/// <summary>
/// Выбирает ClickHouse-тип для поля доменной схемы.
/// </summary>
internal sealed class ClickHouseColumnTypeResolver
{
    public ClickHouseColumnTypeResolver(ClickHouseWriteOptions options)
    {
        _ = options;
    }

    public string Resolve(DataField field)
    {
        var type = field.DataType switch
        {
            DataType.Text => "String",
            DataType.Integer => ResolveInteger(field),
            DataType.Number => ResolveNumber(field),
            DataType.DateTime => "DateTime64(3)",
            DataType.Date => "Date",
            DataType.Time => "DateTime",
            DataType.Boolean => "Bool",
            _ => throw new ArgumentOutOfRangeException(nameof(field), field.DataType, null)
        };

        return field.AllowDBNull ?? true
            ? $"Nullable({type})"
            : type;
    }

    private static string ResolveInteger(DataField field)
    {
        if (field.ClrType == typeof(BigInteger))
        {
            return "Int256";
        }

        return field.ClrType switch
        {
            var type when type == typeof(byte) => "UInt8",
            var type when type == typeof(sbyte) => "Int8",
            var type when type == typeof(short) => "Int16",
            var type when type == typeof(ushort) => "UInt16",
            var type when type == typeof(int) => "Int32",
            var type when type == typeof(uint) => "UInt32",
            var type when type == typeof(long) => "Int64",
            var type when type == typeof(ulong) => "UInt64",
            _ => "Int64"
        };
    }

    private static string ResolveNumber(DataField field)
    {
        if (field.ClrType == typeof(float))
        {
            return "Float32";
        }

        if (field.ClrType == typeof(double))
        {
            return "Float64";
        }

        var shape = NumericShape.Normalize(field.NumericPrecision, field.NumericScale);
        if (shape is { Precision: { } precision, Scale: { } scale })
        {
            return $"Decimal({precision}, {scale})";
        }

        return field.ClrType == typeof(decimal) || field.ClrType == typeof(ClickHouseDecimal)
            ? "Decimal(38, 10)"
            : "Float64";
    }
}

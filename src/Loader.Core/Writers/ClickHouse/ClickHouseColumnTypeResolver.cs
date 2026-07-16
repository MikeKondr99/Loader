using Loader.Core.Decorators;
using Loader.Core.Models;

namespace Loader.Core.Writers.ClickHouse;

/// <summary>
/// Выбирает ClickHouse-тип для поля доменной схемы.
/// Метаданные используются только как оптимизация: nullable, numeric bounds, decimal precision/scale и cardinality.
/// </summary>
internal sealed class ClickHouseColumnTypeResolver
{
    private readonly ClickHouseWriteOptions _options;

    public ClickHouseColumnTypeResolver(ClickHouseWriteOptions options)
    {
        _options = options;
    }

    public string Resolve(DataField field, DataColumnMeta? meta)
    {
        // 1. Выбираем базовый тип без Nullable.
        var type = field.DataType switch
        {
            DataType.Text => ResolveText(meta),
            DataType.Integer => ResolveInteger(field, meta),
            DataType.Number => ResolveNumber(field, meta),
            DataType.DateTime => "DateTime64(3)",
            DataType.Date => "Date",
            DataType.Time => "String",
            DataType.Boolean => "Bool",
            _ => throw new ArgumentOutOfRangeException(nameof(field), field.DataType, null)
        };

        // 2. Nullable добавляем поверх базового типа, если схема или meta допускают null.
        if (!ShouldBeNullable(field, meta))
        {
            return type;
        }

        return type == "LowCardinality(String)"
            ? "LowCardinality(Nullable(String))"
            : $"Nullable({type})";
    }

    private string ResolveText(DataColumnMeta? meta)
    {
        if (_options.UseLowCardinalityForText &&
            meta is { CardinalityExceeded: false, UniqueValueCount: > 0 })
        {
            return "LowCardinality(String)";
        }

        return "String";
    }

    private static string ResolveInteger(DataField field, DataColumnMeta? meta)
    {
        if (meta?.Min is not null && meta.Max is not null)
        {
            return ResolveIntegerByBounds(meta.Min.Value, meta.Max.Value);
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

    private static string ResolveIntegerByBounds(decimal min, decimal max)
    {
        if (min >= 0)
        {
            if (max <= byte.MaxValue)
            {
                return "UInt8";
            }

            if (max <= ushort.MaxValue)
            {
                return "UInt16";
            }

            if (max <= uint.MaxValue)
            {
                return "UInt32";
            }

            return "UInt64";
        }

        if (min >= sbyte.MinValue && max <= sbyte.MaxValue)
        {
            return "Int8";
        }

        if (min >= short.MinValue && max <= short.MaxValue)
        {
            return "Int16";
        }

        if (min >= int.MinValue && max <= int.MaxValue)
        {
            return "Int32";
        }

        return "Int64";
    }

    private static string ResolveNumber(DataField field, DataColumnMeta? meta)
    {
        if (field.ClrType == typeof(float))
        {
            return "Float32";
        }

        if (field.ClrType == typeof(double))
        {
            return "Float64";
        }

        var precision = meta?.DecimalPrecision ?? field.NumericPrecision;
        var scale = meta?.DecimalScale ?? field.NumericScale;
        if (precision is not null && scale is not null)
        {
            return $"Decimal({precision.Value}, {scale.Value})";
        }

        return field.ClrType == typeof(decimal)
            ? "Decimal(38, 10)"
            : "Float64";
    }

    private static bool ShouldBeNullable(DataField field, DataColumnMeta? meta)
    {
        if (meta is not null)
        {
            return meta.Density < 1m;
        }

        if (field.AllowDBNull is true)
        {
            return true;
        }

        return false;
    }
}

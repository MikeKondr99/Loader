using System.Data.Common;
using Loader.Core.Decorators;

namespace Loader.Demo;

/// <summary>
/// Дает исходному reader-у безопасные физические имена column1, column2 и так далее.
/// Значения и типизированные accessor-ы остаются provider-native.
/// </summary>
internal sealed class PhysicalColumnDataReader : DbDataReaderDecorator
{
    private readonly string[] _names;

    public PhysicalColumnDataReader(DbDataReader inner)
        : base(inner)
    {
        OriginalNames = Enumerable.Range(0, inner.FieldCount).Select(inner.GetName).ToArray();
        _names = Enumerable.Range(1, inner.FieldCount).Select(static ordinal => $"column{ordinal}").ToArray();
    }

    public IReadOnlyList<string> OriginalNames { get; }

    public override string GetName(int ordinal)
    {
        return ordinal >= 0 && ordinal < _names.Length
            ? _names[ordinal]
            : throw new IndexOutOfRangeException($"Индекс колонки {ordinal} вне диапазона.");
    }

    public override int GetOrdinal(string name)
    {
        for (var ordinal = 0; ordinal < _names.Length; ordinal++)
        {
            if (string.Equals(_names[ordinal], name, StringComparison.Ordinal))
            {
                return ordinal;
            }
        }

        throw new IndexOutOfRangeException($"Колонка '{name}' не найдена.");
    }
}

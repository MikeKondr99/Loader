using System.Data.Common;

namespace Loader.Core.Decorators;

/// <summary>
/// Заменяет имена колонок reader-а на переданные имена.
/// Значения и typed accessors остаются provider-native.
/// </summary>
public sealed class RenameColumnDataReader : DbDataReaderDecorator
{
    private readonly IReadOnlyList<string> _names;

    public RenameColumnDataReader(DbDataReader inner, IReadOnlyList<string> names)
        : base(inner)
    {
        if (names.Count != inner.FieldCount)
        {
            throw new ArgumentException(
                $"Column names count {names.Count} does not match reader field count {inner.FieldCount}.",
                nameof(names));
        }

        OriginalNames = Enumerable.Range(0, inner.FieldCount).Select(inner.GetName).ToArray();

        var duplicate = names
            .GroupBy(static name => name, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException($"Column name '{duplicate.Key}' is duplicated.", nameof(names));
        }

        _names = names;
    }

    public IReadOnlyList<string> OriginalNames { get; }

    public override string GetName(int ordinal)
    {
        return ordinal >= 0 && ordinal < _names.Count
            ? _names[ordinal]
            : throw new IndexOutOfRangeException($"Индекс колонки {ordinal} вне диапазона.");
    }

    public override int GetOrdinal(string name)
    {
        for (var ordinal = 0; ordinal < _names.Count; ordinal++)
        {
            if (string.Equals(_names[ordinal], name, StringComparison.Ordinal))
            {
                return ordinal;
            }
        }

        throw new IndexOutOfRangeException($"Колонка '{name}' не найдена.");
    }
}

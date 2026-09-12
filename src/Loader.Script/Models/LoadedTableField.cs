using Loader.Core.Models;

namespace Loader.Script;

/// <summary>
/// Metadata одного поля загруженной таблицы.
/// </summary>
public sealed record LoadedTableField
{
    /// <summary>
    /// Имя поля.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Доменный тип поля.
    /// </summary>
    public required DataType DataType { get; init; }

    /// <summary>
    /// Количество уникальных значений.
    /// </summary>
    public long? Cardinality { get; init; }

    /// <summary>
    /// Количество non-null значений.
    /// </summary>
    public long? Density { get; init; }

    /// <summary>
    /// Может ли поле содержать null.
    /// </summary>
    public bool CanBeNull { get; init; }

    /// <summary>
    /// Минимальное значение. Ожидаемые типы: <see cref="decimal"/>, <see cref="long"/>,
    /// <see cref="string"/>, <see cref="DateTime"/>, <see cref="DateOnly"/>.
    /// </summary>
    public object? Min { get; init; }

    /// <summary>
    /// Максимальное значение. Ожидаемые типы: <see cref="decimal"/>, <see cref="long"/>,
    /// <see cref="string"/>, <see cref="DateTime"/>, <see cref="DateOnly"/>.
    /// </summary>
    public object? Max { get; init; }

}

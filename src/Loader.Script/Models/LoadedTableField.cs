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

    /// <summary>
    /// Максимальная длина строки.
    /// </summary>
    public int? StringMaxLength { get; init; }

    public T? GetMin<T>()
        where T : class
    {
        return (T?)GetValue(Min, typeof(T), nameof(Min));
    }

    public T? GetMin<T>(T? _ = null)
        where T : struct
    {
        var value = GetValue(Min, typeof(T), nameof(Min));
        return value is null ? null : (T)value;
    }

    public T? GetMax<T>()
        where T : class
    {
        return (T?)GetValue(Max, typeof(T), nameof(Max));
    }

    public T? GetMax<T>(T? _ = null)
        where T : struct
    {
        var value = GetValue(Max, typeof(T), nameof(Max));
        return value is null ? null : (T)value;
    }

    //  TODO: поправить какая-то дичь
    private static object? GetValue(object? value, Type expectedType, string name)
    {
        if (value is null)
        {
            return null;
        }

        if (expectedType.IsInstanceOfType(value))
        {
            return value;
        }

        throw new InvalidCastException($"{name} has type '{value.GetType().Name}', not '{expectedType.Name}'.");
    }
}

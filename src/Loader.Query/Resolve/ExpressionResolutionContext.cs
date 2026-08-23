using Loader.Lang;
using Loader.Query.Models;

namespace Loader.Query.Resolve;

/// <summary>
/// Контекст для resolve-а выражений.
/// </summary>
public class ExpressionResolutionContext
{
    private readonly List<LangError> errors = [];

    public static ExpressionResolutionContext Empty => new();

    public IReadOnlyList<LangError> Errors => errors;

    /// <summary>
    /// Возвращает metadata mapping-таблицы для script-aware функции Map.
    /// Базовая реализация ничего не знает о mapping-таблицах.
    /// </summary>
    public virtual MapTableInfo? GetMap(string name)
    {
        return null;
    }

    /// <summary>
    /// Возвращает true, если внешний context знает таблицу с таким именем, но она не обязательно является mapping-таблицей.
    /// Нужен, чтобы script-aware функции могли отличить отсутствующую таблицу от таблицы неверного вида.
    /// </summary>
    public virtual bool HasTable(string name)
    {
        return false;
    }

    public void AddError(LangError error)
    {
        if (errors.Any(existing =>
                existing.Message == error.Message &&
                Nullable.Equals(existing.Span, error.Span)))
        {
            return;
        }

        errors.Add(error);
    }
}

/// <summary>
/// Минимальная metadata mapping-таблицы, достаточная для resolve-а функции Map.
/// </summary>
public sealed record MapTableInfo
{
    /// <summary>
    /// Пользовательское имя mapping-таблицы в script.
    /// </summary>
    public required string Alias { get; init; }

    /// <summary>
    /// Физическое имя ClickHouse Join-table, которое можно передать в joinGetOrNull.
    /// </summary>
    public required string PhysicalTableName { get; init; }

    /// <summary>
    /// Тип key-колонки mapping-таблицы.
    /// </summary>
    public required FieldType KeyType { get; init; }

    /// <summary>
    /// Тип value-колонки mapping-таблицы.
    /// </summary>
    public required FieldType ValueType { get; init; }
}

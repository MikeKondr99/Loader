using Loader.Core.Writers.ClickHouse;

namespace Loader.Script;

/// <summary>
/// Таблица, которая была получена после выполнения части script.
/// </summary>
public sealed record LoadedTable
{
    /// <summary>
    /// Физическое имя таблицы в ClickHouse.
    /// </summary>
    public required ClickHouseTableName Name { get; init; }

    /// <summary>
    /// Доменное имя таблицы из script, если оно задано.
    /// </summary>
    public required string? Alias { get; init; }

    /// <summary>
    /// Количество строк в таблице, если оно известно.
    /// </summary>
    public long? RowCount { get; init; }

    /// <summary>
    /// Поля таблицы в исходном порядке.
    /// </summary>
    public required List<LoadedTableField> Fields { get; init; }
}

namespace Loader.Script;

/// <summary>
/// Таблица, которая была получена после выполнения части script.
/// </summary>
public sealed record LoadedTable
{
    /// <summary>
    /// Имя таблицы, если оно известно. <c>null</c> означает, что имя еще не назначено.
    /// </summary>
    public required string? Name { get; init; }

    /// <summary>
    /// Количество строк в таблице, если оно известно.
    /// </summary>
    public long? RowCount { get; init; }

    /// <summary>
    /// Поля таблицы в исходном порядке.
    /// </summary>
    public required List<LoadedTableField> Fields { get; init; }
}

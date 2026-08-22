namespace Loader.Script;

/// <summary>
/// Runtime-вид таблицы, созданной при выполнении script.
/// </summary>
public enum LoadedTableKind
{
    /// <summary>
    /// Пользовательская таблица, возвращаемая после успешного выполнения script.
    /// </summary>
    Normal,

    /// <summary>
    /// Промежуточная таблица, доступная во время выполнения script и удаляемая cleanup-ом.
    /// </summary>
    Temp,

    /// <summary>
    /// Mapping-таблица, используемая функцией Map и удаляемая cleanup-ом.
    /// </summary>
    Mapped
}

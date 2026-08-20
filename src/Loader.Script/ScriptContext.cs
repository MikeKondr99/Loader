using Loader.Core.Sources;

namespace Loader.Script;

/// <summary>
/// Runtime context выполнения Loader script.
/// Хранит стартовые зависимости, например file storage и целевое ClickHouse-подключение,
/// и накапливает состояние выполнения, например имена уже созданных финальных таблиц.
/// </summary>
public sealed record ScriptContext
{
    private readonly List<LoadedTable> _loadedTables = [];

    /// <summary>
    /// Файловая абстракция, через которую file providers будут открывать источники из script.
    /// В тестах сюда можно передать in-memory или temp-root реализацию вместо реальных process paths.
    /// </summary>
    public required IFileSource FileStorage { get; init; }

    /// <summary>
    /// ClickHouse connection string, куда выполнение script пишет финальные таблицы.
    /// </summary>
    public required string TargetConnectionString { get; init; }

    public IConnectionRegistry ConnectionRegistry { get; init; } = EmptyConnectionRegistry.Instance;

    public ScriptContextOptions Options { get; init; } = new();

    /// <summary>
    /// Структурный logger пользовательского progress выполнения script.
    /// </summary>
    public IProgressLogger Logger { get; init; } = NullProgressLogger.Instance;

    /// <summary>
    /// Финальные таблицы, которые script execution уже успешно создал.
    /// </summary>
    public IReadOnlyList<LoadedTable> LoadedTables => _loadedTables;

    public void AddLoadedTable(LoadedTable table)
    {
        _loadedTables.Add(table);
    }

    public LoadedTable? FindLoadedTable(string alias)
    {
        return _loadedTables.SingleOrDefault(table => string.Equals(
            table.Alias,
            alias,
            StringComparison.Ordinal));
    }

    public void RemoveLoadedTable(LoadedTable table)
    {
        _loadedTables.Remove(table);
    }
}

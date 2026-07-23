using Loader.Core.Sources;

namespace Loader.Script;

/// <summary>
/// Runtime context выполнения Loader script.
/// Хранит стартовые зависимости, например file storage и целевое ClickHouse-подключение,
/// и накапливает состояние выполнения, например имена уже созданных финальных таблиц.
/// </summary>
public sealed class ScriptContext
{
    private readonly List<string> _loadedTables = [];

    /// <summary>
    /// Файловая абстракция, через которую file providers будут открывать источники из script.
    /// В тестах сюда можно передать in-memory или temp-root реализацию вместо реальных process paths.
    /// </summary>
    public required IFileSource FileStorage { get; init; }

    /// <summary>
    /// ClickHouse connection string, куда выполнение script пишет финальные таблицы.
    /// </summary>
    public required string TargetConnectionString { get; init; }

    /// <summary>
    /// Имена финальных таблиц, которые script execution уже успешно создал.
    /// </summary>
    public IReadOnlyList<string> LoadedTables => _loadedTables;

    public void AddLoadedTable(string tableName)
    {
        if (tableName.Trim().Length == 0)
        {
            throw new ArgumentException("Table name must not be empty.", nameof(tableName));
        }

        _loadedTables.Add(tableName);
    }
}

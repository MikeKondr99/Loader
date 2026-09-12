using System.Security.Cryptography;
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
    private int _sqlAliasIndex;

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
        if (table.Alias is not null && ContainsLoadedTable(table.Alias))
        {
            throw new QueryResolutionException($"Имя LOAD таблицы '{table.Alias}' уже занято.");
        }

        _loadedTables.Add(table);
    }

    public bool ContainsLoadedTable(string alias)
    {
        return _loadedTables.Any(table => string.Equals(
            table.Alias,
            alias,
            StringComparison.Ordinal));
    }

    public LoadedTable? FindLoadedTable(string alias)
    {
        return _loadedTables.FirstOrDefault(table => string.Equals(
            table.Alias,
            alias,
            StringComparison.Ordinal));
    }

    public void RemoveLoadedTable(LoadedTable table)
    {
        _loadedTables.Remove(table);
    }

    /// <summary>
    /// Создает короткое физическое имя ClickHouse-таблицы с заданным префиксом.
    /// Префикс задает внешний scope, например <c>lt_user_</c> или <c>lf_user_</c>,
    /// а random suffix защищает от пересечений со старыми таблицами после restart/crash.
    /// </summary>
    public string CreatePhysicalTableName(string prefix)
    {
        return $"{prefix}{CreateDenseRandom(5)}";
    }

    /// <summary>
    /// Создает короткий SQL alias, уникальный внутри одного script execution.
    /// Для alias достаточно счетчика: они живут только внутри скомпилированных запросов.
    /// </summary>
    public string CreateSqlAlias(string prefix)
    {
        var index = Interlocked.Increment(ref _sqlAliasIndex);
        return $"{prefix}{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// Кодирует random bytes в компактный lowercase base32 без спецсимволов.
    /// При <c>byteCount = 5</c> получается 8 символов и 40 бит энтропии.
    /// </summary>
    private static string CreateDenseRandom(int byteCount)
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyz234567";
        Span<byte> bytes = stackalloc byte[byteCount];
        RandomNumberGenerator.Fill(bytes);

        Span<char> chars = stackalloc char[(byteCount * 8 + 4) / 5];
        var buffer = 0;
        var bitsLeft = 0;
        var charIndex = 0;

        foreach (var value in bytes)
        {
            buffer = (buffer << 8) | value;
            bitsLeft += 8;

            while (bitsLeft >= 5)
            {
                chars[charIndex++] = alphabet[(buffer >> (bitsLeft - 5)) & 31];
                bitsLeft -= 5;
            }
        }

        if (bitsLeft > 0)
        {
            chars[charIndex++] = alphabet[(buffer << (5 - bitsLeft)) & 31];
        }

        return new string(chars[..charIndex]);
    }
}

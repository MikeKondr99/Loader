using Loader.Core.Models;

namespace Loader.Script.Execution;

/// <summary>
/// Подготовленный источник строк для LOAD query.
/// Executor работает только с этой моделью и не знает, был ли source залит в temp table или представлен SQL напрямую.
/// </summary>
internal sealed class PreparedLoadSource : IAsyncDisposable
{
    private readonly Func<ValueTask> disposeAsync;
    private bool disposed;

    /// <summary>
    /// Создает подготовленный source для LOAD.
    /// Если cleanup не передан, source считается виртуальным SQL-source-ом без физического ресурса на очистку.
    /// </summary>
    public PreparedLoadSource(
        string sql,
        string alias,
        IReadOnlyList<PreparedLoadSourceField> fields,
        Func<ValueTask>? disposeAsync = null)
    {
        Sql = sql;
        Alias = alias;
        Fields = fields;
        this.disposeAsync = disposeAsync ?? (() => ValueTask.CompletedTask);
    }

    /// <summary>
    /// SQL-выражение для части FROM. Может быть именем таблицы или подзапросом в скобках.
    /// </summary>
    public string Sql { get; }

    /// <summary>
    /// Уникальный SQL alias source-а. Все обращения к physical columns должны идти через него.
    /// </summary>
    public string Alias { get; }

    /// <summary>
    /// Поля source-а с привязкой пользовательского имени к физической SQL-колонке.
    /// </summary>
    public IReadOnlyList<PreparedLoadSourceField> Fields { get; }

    /// <summary>
    /// Освобождает ресурсы prepared source-а.
    /// Для temp table удаляет физическую таблицу, для прямого SQL-source-а ничего не делает.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await disposeAsync().ConfigureAwait(false);
    }
}

/// <summary>
/// Поле подготовленного source-а, доступное выражениям LOAD/WHERE/GROUP/ORDER.
/// </summary>
internal sealed record PreparedLoadSourceField
{
    /// <summary>
    /// Имя поля в доменном скрипте.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Имя колонки внутри SQL-source.
    /// </summary>
    public required string PhysicalName { get; init; }

    /// <summary>
    /// Доменный тип поля.
    /// </summary>
    public required DataType DataType { get; init; }

    /// <summary>
    /// Может ли поле возвращать null.
    /// </summary>
    public required bool CanBeNull { get; init; }
}

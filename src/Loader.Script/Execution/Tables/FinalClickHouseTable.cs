using Loader.Core.Writers.ClickHouse;

namespace Loader.Script;

/// <summary>
/// Владелец физической final table во время выполнения одного LOAD.
/// Если таблица не была подтверждена через <see cref="Commit"/>, dispose выполняет rollback cleanup.
/// </summary>
public sealed class FinalClickHouseTable : IAsyncDisposable
{
    private readonly Func<ValueTask> rollbackAsync;
    private bool committed;
    private bool disposed;

    internal FinalClickHouseTable(ClickHouseTableName tableName, Func<ValueTask> rollbackAsync)
    {
        TableName = tableName;
        this.rollbackAsync = rollbackAsync;
    }

    /// <summary>
    /// Физическое имя ClickHouse-таблицы, в которую материализуется результат LOAD.
    /// </summary>
    public ClickHouseTableName TableName { get; }

    /// <summary>
    /// Подтверждает, что final table успешно записана и не должна удаляться при dispose.
    /// </summary>
    public void Commit()
    {
        committed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (!committed)
        {
            await rollbackAsync().ConfigureAwait(false);
        }
    }
}

using Loader.Core.Writers.ClickHouse;

namespace Loader.Script;

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

    public ClickHouseTableName TableName { get; }

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

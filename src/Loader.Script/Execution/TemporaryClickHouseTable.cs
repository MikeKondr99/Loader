using Loader.Core.Models;
using Loader.Core.Writers.ClickHouse;

namespace Loader.Script;

public sealed class TemporaryClickHouseTable : IAsyncDisposable
{
    private readonly Func<ValueTask> disposeAsync;
    private bool disposed;

    internal TemporaryClickHouseTable(
        ClickHouseTableName tableName,
        DataSchema schema,
        IReadOnlyList<string> originalColumnNames,
        Func<ValueTask> disposeAsync)
    {
        TableName = tableName;
        Schema = schema;
        OriginalColumnNames = originalColumnNames;
        this.disposeAsync = disposeAsync;
    }

    public ClickHouseTableName TableName { get; }

    public DataSchema Schema { get; }

    public IReadOnlyList<string> OriginalColumnNames { get; }

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

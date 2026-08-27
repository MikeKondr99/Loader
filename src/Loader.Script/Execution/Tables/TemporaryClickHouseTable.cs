using Loader.Core.Models;
using Loader.Core.Writers.ClickHouse;

namespace Loader.Script;

/// <summary>
/// Владелец physical temp table, созданной для stage-данных одного LOAD.
/// Temp table всегда удаляется при dispose, потому что дальше нужна только final table.
/// </summary>
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

    /// <summary>
    /// Физическое имя временной ClickHouse-таблицы.
    /// </summary>
    public ClickHouseTableName TableName { get; }

    /// <summary>
    /// Schema временной таблицы после нормализации типов и переименования колонок в <c>columnN</c>.
    /// </summary>
    public DataSchema Schema { get; }

    /// <summary>
    /// Доменные имена колонок до замены на <c>column1</c>, <c>column2</c>...
    /// Нужны следующему шагу, чтобы собрать <c>QuerySource</c> с пользовательскими aliases.
    /// </summary>
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

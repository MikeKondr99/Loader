using System.Text;
using ClickHouse.Client.ADO;
using Loader.Core.Writers.ClickHouse;

namespace Loader.Script.Execution;

/// <summary>
/// Удаляет физические ClickHouse-таблицы, созданные через <c>TEMP LOAD</c>, и убирает их из script context.
/// </summary>
public class TemporaryLoadedTableCleanupExecutor
{
    /// <summary>
    /// Удаляет все временные loaded tables, которые сейчас зарегистрированы в context.
    /// </summary>
    public virtual async ValueTask ExecuteAsync(
        ScriptContext context,
        CancellationToken cancellationToken = default)
    {
        var temporaryTables = context.LoadedTables
            .Where(static table => table.Kind == LoadedTableKind.Temp)
            .ToArray();

        if (temporaryTables.Length > 0)
        {
            await context.Logger.TempLoadCleanupStartedAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var table in temporaryTables)
        {
            await context.Logger.DropTableStartedAsync(
                    table.Alias ?? table.Name.Table,
                    cancellationToken)
                .ConfigureAwait(false);
            await DropTableAsync(context, table.Name, cancellationToken).ConfigureAwait(false);
            context.RemoveLoadedTable(table);
        }
    }

    /// <summary>
    /// Best-effort cleanup после ошибки script; ошибки cleanup не должны скрывать исходную ошибку.
    /// </summary>
    public virtual async ValueTask ExecuteBestEffortAsync(ScriptContext context)
    {
        try
        {
            await ExecuteAsync(context, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Cleanup must not hide the original script failure.
        }
    }

    /// <summary>
    /// Удаляет одну физическую ClickHouse-таблицу.
    /// </summary>
    protected virtual async ValueTask DropTableAsync(
        ScriptContext context,
        ClickHouseTableName tableName,
        CancellationToken cancellationToken)
    {
        await using var connection = new ClickHouseConnection(context.TargetConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = new StringBuilder()
            .Append("DROP TABLE IF EXISTS ")
            .Append(tableName.ToSql())
            .ToString();
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

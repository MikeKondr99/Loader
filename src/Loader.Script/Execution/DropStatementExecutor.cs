using System.Text;
using ClickHouse.Client.ADO;
using Loader.Core.Writers.ClickHouse;
using Loader.Lang.Statements;

namespace Loader.Script.Execution;

public class DropStatementExecutor
{
    public async ValueTask ExecuteAsync(
        ScriptContext context,
        DropStatement statement,
        CancellationToken cancellationToken = default)
    {
        var table = context.FindLoadedTable(statement.Name);
        if (table is null)
        {
            LoadScriptTelemetry.GetCurrentActivity()?
                .SetTag("drop.found", false);
            throw new LoadScriptExecutionException(
                LoadScriptStage.DropTable,
                $"Таблица '{statement.Name}' не найдена среди загруженных LOAD таблиц.",
                statement.NameSpan);
        }

        LoadScriptTelemetry.GetCurrentActivity()?
            .SetTag("drop.found", true)
            .SetTag("drop.physical_table", table.Name.Table);

        try
        {
            await context.Logger.DropTableStartedAsync(statement.Name, cancellationToken).ConfigureAwait(false);
            await DropTableAsync(context, table.Name, cancellationToken).ConfigureAwait(false);
        }
        catch (LoadScriptStageException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new LoadScriptExecutionException(
                LoadScriptStage.DropTable,
                $"Не удалось удалить таблицу '{statement.Name}': {exception.Message}",
                statement.NameSpan,
                exception);
        }

        context.RemoveLoadedTable(table);
    }

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

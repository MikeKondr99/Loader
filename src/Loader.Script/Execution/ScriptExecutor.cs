using Loader.Lang.Statements;

namespace Loader.Script.Execution;

/// <summary>
/// Исполнитель скрипта без собственного состояния. Состояние выполнения накапливается в <see cref="ScriptContext"/>.
/// </summary>
public sealed class ScriptExecutor
{
    public LoadStatementExecutor LoadStatementExecutor { get; init; } = new();

    public DropStatementExecutor DropStatementExecutor { get; init; } = new();

    public async ValueTask<IReadOnlyList<LoadedTable>> ExecuteAsync(
        ScriptContext context,
        Loader.Lang.Script script,
        CancellationToken cancellationToken = default)
    {
        for (var index = 0; index < script.Statements.Count; index++)
        {
            var statement = script.Statements[index];
            using var activity = LoadScriptTelemetry.ActivitySource.StartActivity("Script.Statement");
            activity?
                .SetTag("script.statement.index", index)
                .SetTag("script.statement.type", statement.GetType().Name);
            if (statement is LoadStatement load)
            {
                activity?
                    .SetTag("load.table_name", load.TableName)
                    .SetTag("load.source_provider", load.SourceCall.Name);
            }
            else if (statement is DropStatement drop)
            {
                activity?
                    .SetTag("drop.table_name", drop.Name);
            }

            try
            {
                await ExecuteStatementAsync(context, statement, cancellationToken).ConfigureAwait(false);
            }
            catch (LoadScriptException)
            {
                throw;
            }
            catch (LoadScriptStageException exception)
            {
                throw new LoadScriptException(index, statement, exception);
            }
        }

        return context.LoadedTables;
    }

    private async ValueTask ExecuteStatementAsync(
        ScriptContext context,
        Statement statement,
        CancellationToken cancellationToken)
    {
        switch (statement)
        {
            case LoadStatement load:
                await LoadStatementExecutor.ExecuteAsync(context, load, cancellationToken).ConfigureAwait(false);
                return;

            case DropStatement drop:
                await DropStatementExecutor.ExecuteAsync(context, drop, cancellationToken).ConfigureAwait(false);
                return;

            default:
                throw new NotSupportedException($"Statement '{statement.GetType().Name}' не поддерживается.");
        }
    }
}

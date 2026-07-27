using Loader.Lang.Statements;

namespace Loader.Script.Execution;

public sealed class ScriptExecutor
{
    public LoadStatementExecutor LoadStatementExecutor { get; init; } = new();

    public async ValueTask<IReadOnlyList<LoadedTable>> ExecuteAsync(
        ScriptContext context,
        Loader.Lang.Script script,
        CancellationToken cancellationToken = default)
    {
        for (var index = 0; index < script.Statements.Count; index++)
        {
            var statement = script.Statements[index];
            using var activity = LoadScriptTelemetry.ActivitySource.StartActivity("Script.Statement");
            activity?.SetTag("script.statement.index", index);
            activity?.SetTag("script.statement.type", statement.GetType().Name);
            if (statement is LoadStatement load)
            {
                activity?.SetTag("load.table_name", load.TableName);
                activity?.SetTag("load.source", LoadScriptTelemetry.RedactSource(load.Source));
            }

            await ExecuteStatementAsync(context, statement, cancellationToken).ConfigureAwait(false);
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

            default:
                throw new NotSupportedException($"Statement '{statement.GetType().Name}' не поддерживается.");
        }
    }
}

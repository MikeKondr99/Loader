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
        foreach (var statement in script.Statements)
        {
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

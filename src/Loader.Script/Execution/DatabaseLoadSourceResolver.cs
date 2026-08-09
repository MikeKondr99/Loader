using Loader.Lang;
using Loader.Lang.Statements;

namespace Loader.Script.Execution;

internal abstract class DatabaseLoadSourceResolver : LoadSourceResolverBase
{
    private readonly DatabaseLoadProviderFactory factory;

    protected DatabaseLoadSourceResolver(ScriptConnectionType provider)
    {
        if (!DatabaseLoadProviderFactory.TryGet(provider, out factory))
        {
            throw new ArgumentException($"Unknown database load provider '{provider}'.", nameof(provider));
        }
    }

    public override ValueTask<LoadProviderSource> ResolveAsync(
        LoadStatement statement,
        ScriptContext context,
        LoadOptionReader options,
        List<LangError> errors,
        CancellationToken cancellationToken)
    {
        RejectUnknownOptions(statement.SourceCall.Name, options, errors, ["connection"]);
        var connection = RequiredConnection(factory.Kind, statement, options, errors);
        var sql = SourceSql(factory.Kind, statement, errors);
        if (connection is null || errors.Count > 0)
        {
            return ValueTask.FromResult<LoadProviderSource>(null!);
        }

        return ValueTask.FromResult(factory.CreateSource(connection, sql!));
    }
}

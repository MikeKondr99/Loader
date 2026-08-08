using System.Data.Common;
using Loader.Core.Providers.Sql;
using Loader.Core.Sources;
using Loader.Lang;
using Loader.Lang.Statements;

namespace Loader.Script.Execution;

internal abstract class DatabaseLoadSourceResolver : LoadSourceResolverBase
{
    private readonly string kind;
    private readonly bool requiresBuffer;
    private readonly Func<IDatabaseSource, SqlTableConfig, CancellationToken, ValueTask<DbDataReader>> open;

    protected DatabaseLoadSourceResolver(
        string kind,
        bool requiresBuffer,
        Func<IDatabaseSource, SqlTableConfig, CancellationToken, ValueTask<DbDataReader>> open)
    {
        this.kind = kind;
        this.requiresBuffer = requiresBuffer;
        this.open = open;
    }

    public override ValueTask<LoadProviderSource> ResolveAsync(
        LoadStatement statement,
        ScriptContext context,
        LoadOptionReader options,
        List<LangError> errors,
        CancellationToken cancellationToken)
    {
        var connection = RequiredConnection(kind, statement, options, errors);
        var sql = SourceSql(kind, statement, errors);
        if (connection is null || errors.Count > 0)
        {
            return ValueTask.FromResult<LoadProviderSource>(null!);
        }

        var source = new ConnectionStringSource { ConnectionString = connection };
        var config = new SqlTableConfig { Sql = sql! };
        return ValueTask.FromResult(new LoadProviderSource
        {
            Kind = kind,
            RequiresBuffer = requiresBuffer,
            OpenReaderAsync = token => open(source, config, token)
        });
    }
}

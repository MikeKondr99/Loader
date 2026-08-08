using Loader.Core.Providers.SqlServer;

namespace Loader.Script.Execution;

internal sealed class SqlServerLoadSourceResolver : DatabaseLoadSourceResolver
{
    public SqlServerLoadSourceResolver()
        : base(
            "sqlserver",
            requiresBuffer: true,
            static (source, config, token) => new SqlServerProvider().OpenReaderAsync(source, config, token))
    {
    }

    public override string Name => "SqlServer";
}

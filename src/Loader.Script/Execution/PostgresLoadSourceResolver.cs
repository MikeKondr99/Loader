using Loader.Core.Providers.Postgres;

namespace Loader.Script.Execution;

internal sealed class PostgresLoadSourceResolver : DatabaseLoadSourceResolver
{
    public PostgresLoadSourceResolver()
        : base(
            "postgres",
            requiresBuffer: false,
            static (source, config, token) => new PostgresProvider().OpenReaderAsync(source, config, token))
    {
    }

    public override string Name => "Postgres";
}

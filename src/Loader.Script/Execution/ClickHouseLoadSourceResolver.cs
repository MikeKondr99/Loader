using Loader.Core.Providers.ClickHouse;

namespace Loader.Script.Execution;

internal sealed class ClickHouseLoadSourceResolver : DatabaseLoadSourceResolver
{
    public ClickHouseLoadSourceResolver()
        : base(
            "clickhouse",
            requiresBuffer: false,
            static (source, config, token) => new ClickHouseProvider().OpenReaderAsync(source, config, token))
    {
    }

    public override string Name => "ClickHouse";
}

using Loader.Core.Providers.Hive;

namespace Loader.Script.Execution;

internal sealed class HiveLoadSourceResolver : DatabaseLoadSourceResolver
{
    public HiveLoadSourceResolver()
        : base(
            "hive",
            requiresBuffer: true,
            static (source, config, token) => new HiveProvider().OpenReaderAsync(source, config, token))
    {
    }

    public override string Name => "Hive";
}

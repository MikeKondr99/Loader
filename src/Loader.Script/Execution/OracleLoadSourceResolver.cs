using Loader.Core.Providers.Oracle;

namespace Loader.Script.Execution;

internal sealed class OracleLoadSourceResolver : DatabaseLoadSourceResolver
{
    public OracleLoadSourceResolver()
        : base(
            "oracle",
            requiresBuffer: true,
            static (source, config, token) => new OracleProvider().OpenReaderAsync(source, config, token))
    {
    }

    public override string Name => "Oracle";
}

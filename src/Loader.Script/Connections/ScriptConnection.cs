using Loader.Core.Sources;

namespace Loader.Script;

public abstract record ScriptConnection
{
    public required string Name { get; init; }
}

public sealed record DatabaseScriptConnection : ScriptConnection
{
    public required ScriptConnectionType Provider { get; init; }

    public required string ConnectionString { get; init; }
}

public sealed record DwhTableScriptConnection : ScriptConnection
{
    public required string Sql { get; init; }
}

public sealed record FileStorageScriptConnection : ScriptConnection
{
    public required IFileSource Source { get; init; }
}

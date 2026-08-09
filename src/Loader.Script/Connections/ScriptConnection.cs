namespace Loader.Script;

public sealed record ScriptConnection
{
    public required string Name { get; init; }

    public required ScriptConnectionType Provider { get; init; }

    public required string ConnectionString { get; init; }
}

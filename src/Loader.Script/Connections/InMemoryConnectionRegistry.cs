namespace Loader.Script;

public sealed class InMemoryConnectionRegistry : IConnectionRegistry
{
    private readonly IReadOnlyDictionary<string, ScriptConnection> connections;

    public InMemoryConnectionRegistry(IEnumerable<ScriptConnection> connections)
    {
        this.connections = connections.ToDictionary(
            static connection => connection.Name,
            static connection => connection,
            StringComparer.OrdinalIgnoreCase);
    }

    public ValueTask<ScriptConnection?> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(connections.TryGetValue(name, out var connection) ? connection : null);
    }

    public ValueTask<IReadOnlyList<string>> FindNamesAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult<IReadOnlyList<string>>(connections.Keys.ToArray());
    }
}

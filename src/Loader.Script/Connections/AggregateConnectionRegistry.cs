namespace Loader.Script;

public sealed class AggregateConnectionRegistry : IConnectionRegistry
{
    private readonly IReadOnlyList<IConnectionRegistry> registries;

    public AggregateConnectionRegistry(IEnumerable<IConnectionRegistry> registries)
    {
        this.registries = registries.ToArray();
    }

    public async ValueTask<ScriptConnection?> GetAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        foreach (var registry in registries)
        {
            var connection = await registry.GetAsync(name, cancellationToken).ConfigureAwait(false);
            if (connection is not null)
            {
                return connection;
            }
        }

        return null;
    }

    public async ValueTask<IReadOnlyList<string>> FindNamesAsync(CancellationToken cancellationToken = default)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var registry in registries)
        {
            foreach (var name in await registry.FindNamesAsync(cancellationToken).ConfigureAwait(false))
            {
                names.Add(name);
            }
        }

        return names.Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}

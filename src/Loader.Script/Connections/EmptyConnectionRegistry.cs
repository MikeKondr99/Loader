namespace Loader.Script;

public sealed class EmptyConnectionRegistry : IConnectionRegistry
{
    public static readonly EmptyConnectionRegistry Instance = new();

    private EmptyConnectionRegistry()
    {
    }

    public ValueTask<ScriptConnection?> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult<ScriptConnection?>(null);
    }

    public ValueTask<IReadOnlyList<string>> FindNamesAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult<IReadOnlyList<string>>([]);
    }
}

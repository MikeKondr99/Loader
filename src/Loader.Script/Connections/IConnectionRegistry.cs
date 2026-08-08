namespace Loader.Script;

public interface IConnectionRegistry
{
    ValueTask<ScriptConnection?> GetAsync(string name, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<string>> FindNamesAsync(CancellationToken cancellationToken = default);
}

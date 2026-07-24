using Loader.Lang.Statements;

namespace Loader.Script;

public interface ILoadProviderResolver
{
    ValueTask<LoadProviderSource> ResolveAsync(
        LoadStatement statement,
        ScriptContext context,
        CancellationToken cancellationToken = default);
}

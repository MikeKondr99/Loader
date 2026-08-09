using Loader.Lang;
using Loader.Lang.Statements;

namespace Loader.Script.Execution;

internal interface ILoadSourceResolver
{
    string Name { get; }

    ValueTask<LoadProviderSource> ResolveAsync(
        LoadStatement statement,
        ScriptContext context,
        LoadOptionReader options,
        List<LangError> errors,
        CancellationToken cancellationToken);
}

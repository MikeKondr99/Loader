using Loader.Lang;

namespace Loader.Script;

public sealed class QueryResolutionException : LoadScriptStageException
{
    public QueryResolutionException(string message, LangSpan? span = null, Exception? innerException = null)
        : base(LoadScriptStage.QueryResolution, message, span, innerException)
    {
    }
}

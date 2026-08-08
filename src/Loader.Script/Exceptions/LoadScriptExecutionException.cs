using Loader.Lang;

namespace Loader.Script;

public sealed class LoadScriptExecutionException : LoadScriptStageException
{
    public LoadScriptExecutionException(
        LoadScriptStage stage,
        string message,
        LangSpan? span = null,
        Exception? innerException = null)
        : base(stage, message, span, innerException)
    {
    }
}

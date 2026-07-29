using Loader.Lang;

namespace Loader.Script;

public abstract class LoadScriptStageException : Exception
{
    protected LoadScriptStageException(
        LoadScriptStage stage,
        string message,
        LangSpan? span = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Stage = stage;
        Span = span;
    }

    public LoadScriptStage Stage { get; }

    public LangSpan? Span { get; }
}

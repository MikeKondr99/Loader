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
        Errors = span is null
            ? []
            :
            [
                new LangError
                {
                    Message = message,
                    Span = span.Value
                }
            ];
    }

    protected LoadScriptStageException(
        LoadScriptStage stage,
        string message,
        IReadOnlyList<LangError> errors,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Stage = stage;
        Errors = errors;
    }

    public LoadScriptStage Stage { get; }

    public LangSpan? Span => Errors.Count == 0 ? null : Errors[0].Span;

    public IReadOnlyList<LangError> Errors { get; }
}

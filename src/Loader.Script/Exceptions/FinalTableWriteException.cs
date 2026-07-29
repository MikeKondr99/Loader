namespace Loader.Script;

public sealed class FinalTableWriteException : LoadScriptStageException
{
    public FinalTableWriteException(string message, Exception? innerException = null)
        : base(LoadScriptStage.FinalTableWrite, message, innerException: innerException)
    {
    }
}

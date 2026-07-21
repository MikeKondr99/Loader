using Antlr4.Runtime;

namespace Loader.Lang.Expressions;

internal sealed class TokenErrorListener : IAntlrErrorListener<int>
{
    public void SyntaxError(
        TextWriter output,
        IRecognizer recognizer,
        int offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException e)
    {
        throw new LangErrorException(e)
        {
            Error = new LangError
            {
                Span = new LangSpan((uint)line, (uint)charPositionInLine, 1000, 1000),
                Message = msg
            }
        };
    }
}

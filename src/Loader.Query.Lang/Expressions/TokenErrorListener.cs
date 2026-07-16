using Antlr4.Runtime;

namespace Loader.Query.Lang.Expressions;

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
        throw new ExprErrorException(e)
        {
            Error = new ExprError
            {
                Span = new ExprSpan((uint)line, (uint)charPositionInLine, 1000, 1000),
                Message = msg
            }
        };
    }
}

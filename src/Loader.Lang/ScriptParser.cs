using Antlr4.Runtime;
using Loader.Lang.Expressions;
using Loader.Lang.Statements;

namespace Loader.Lang;

internal sealed class ScriptParser
{
    public static ParseResult<Script> Parse(string text)
    {
        try
        {
            var parser = CreateParser(text);
            var script = Parse(parser.full_script());
            return ParseResult<Script>.Success(script);
        }
        catch (LangErrorException ex)
        {
            return ParseResult<Script>.Failure(ex.Error);
        }
        catch (Exception ex)
        {
            return ParseResult<Script>.Failure(new LangError
            {
                Span = new LangSpan(1, 1, 100, 100),
                Message = ex.Message
            });
        }
    }

    private static Script Parse(LangParser.Full_scriptContext context)
    {
        var statementParser = new StatementParser();
        var statements = context
            .statement()
            .Select(statementParser.Visit)
            .ToList();

        return new Script
        {
            Statements = statements
        };
    }

    private static LangParser CreateParser(string text)
    {
        var input = new AntlrInputStream(text);
        var lexer = new LangLexer(input);
        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(new TokenErrorListener());

        var tokens = new CommonTokenStream(lexer);
        tokens.Fill();

        var parser = new LangParser(tokens);
        parser.RemoveErrorListeners();
        parser.AddErrorListener(new ErrorListener());
        return parser;
    }
}

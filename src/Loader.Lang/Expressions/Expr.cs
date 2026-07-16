using System.Diagnostics;
using Antlr4.Runtime;

namespace Loader.Lang.Expressions;

public abstract record Expr
{
    private static readonly ActivitySource ActivitySource = new("Loader.Lang");
    private int? _hash;

    public ExprSpan Span { get; init; }

    public int Hash => _hash ??= GetHashCode();

    public static ParseResult<Expr> Parse(string text)
    {
        using var activity = ActivitySource.StartActivity("expression parsing");
        activity?.SetTag("expression", text);

        try
        {
            var parser = CreateParser(text);
            var expr = new ExpressionParser().VisitStart(parser.start());
            return ParseResult<Expr>.Success(expr);
        }
        catch (ExprErrorException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.AddException(ex);
            return ParseResult<Expr>.Failure(ex.Error);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.AddException(ex);
            return ParseResult<Expr>.Failure(new ExprError
            {
                Span = new ExprSpan(1, 1, 100, 100),
                Message = ex.Message
            });
        }
    }

    public override int GetHashCode()
    {
        return 17;
    }

    public bool Equivalent(Expr other)
    {
        return Hash == other.Hash;
    }

    public bool NotEquivalent(Expr other)
    {
        return Hash != other.Hash;
    }

    public Expr Replace(Expr pattern, Expr value)
    {
        if (Equivalent(pattern))
        {
            return value;
        }

        if (this is FuncExpr func)
        {
            return new FuncExpr
            {
                Name = func.Name,
                Arguments = func.Arguments.Select(argument => argument.Replace(pattern, value)).ToArray(),
                Span = func.Span,
                Kind = func.Kind
            };
        }

        return this;
    }

    public static string Field(string alias)
    {
        return $"[{alias.Replace("]", @"\]", StringComparison.Ordinal)}]";
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

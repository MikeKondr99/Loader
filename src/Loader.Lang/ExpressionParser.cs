using System.Globalization;
using System.Text.RegularExpressions;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Loader.Lang.Expressions;

namespace Loader.Lang;

internal sealed partial class ExpressionParser : LangParserBaseVisitor<Expr>
{
    public override Expr VisitStart(LangParser.StartContext context)
    {
        return Visit(context.expr());
    }

    public override Expr VisitUnary(LangParser.UnaryContext context)
    {
        if (context.children is [TerminalNodeImpl op, LangParser.Term_exprContext expr])
        {
            return new FuncExpr
            {
                Span = Span(context),
                Name = op.GetText(),
                Arguments = [VisitTerm_expr(expr)],
                Kind = FuncExprKind.Unary
            };
        }

        throw new InvalidOperationException("Invalid unary expression.");
    }

    public override Expr VisitScope(LangParser.ScopeContext context)
    {
        return Visit(context.children[1]);
    }

    public override Expr VisitBinary(LangParser.BinaryContext context)
    {
        if (context.children is [LangParser.ExprContext left, TerminalNodeImpl op, LangParser.ExprContext right])
        {
            return new FuncExpr
            {
                Span = Span(context),
                Name = op.GetText(),
                Arguments = [Visit(left), Visit(right)],
                Kind = FuncExprKind.Binary
            };
        }

        throw new InvalidOperationException("Invalid binary expression.");
    }

    public override Expr VisitObjectFunction(LangParser.ObjectFunctionContext context)
    {
        var args = context.children.OfType<ParserRuleContext>().Select(Visit);
        return new FuncExpr
        {
            Span = Span(context),
            Name = context.children[2].GetText(),
            Arguments = args.ToArray(),
            Kind = FuncExprKind.Method
        };
    }

    public override Expr VisitName(LangParser.NameContext context)
    {
        return new NameExpr(UnescapeName(context.GetText()))
        {
            Span = Span(context)
        };
    }

    public override Expr VisitString(LangParser.StringContext context)
    {
        var expressions = new List<Expr>();
        foreach (var part in context.stringContents())
        {
            var expr = part.children.OfType<LangParser.ExprContext>().FirstOrDefault();

            void Append(Expr value)
            {
                if (expressions.LastOrDefault() is StringLiteral last && value is StringLiteral next)
                {
                    expressions[^1] = new StringLiteral(last.Value + next.Value, Span(part));
                    return;
                }

                expressions.Add(value);
            }

            if (expr is not null)
            {
                var value = Visit(expr);
                Append(new FuncExpr
                {
                    Name = "Text",
                    Arguments = [value],
                    Kind = FuncExprKind.Default,
                    Span = value.Span
                });
            }
            else if (part.TEXT() is not null)
            {
                Append(new StringLiteral(part.GetText(), Span(part)));
            }
            else if (part.ESCAPE_SEQUENCE() is not null)
            {
                var character = part.GetText()[1];
                var value = character switch
                {
                    '\'' => "'",
                    '\\' => "\\",
                    'n' => "\n",
                    'r' => "\r",
                    't' => "\t",
                    '$' => "$",
                    _ => $"\\{character}"
                };
                Append(new StringLiteral(value, Span(part)));
            }
            else
            {
                throw new InvalidOperationException("Invalid string content.");
            }
        }

        if (expressions.Count == 0)
        {
            return new StringLiteral(string.Empty)
            {
                Span = Span(context)
            };
        }

        return expressions.Aggregate((left, right) => new FuncExpr
        {
            Name = "+",
            Kind = FuncExprKind.Binary,
            Arguments = [left, right],
            Span = Span(context)
        });
    }

    public override Expr VisitInteger(LangParser.IntegerContext context)
    {
        return new IntegerLiteral(long.Parse(context.GetText(), CultureInfo.InvariantCulture))
        {
            Span = Span(context)
        };
    }

    public override Expr VisitNumber(LangParser.NumberContext context)
    {
        return new NumberLiteral(double.Parse(context.GetText(), CultureInfo.InvariantCulture))
        {
            Span = Span(context)
        };
    }

    public override Expr VisitFunc(LangParser.FuncContext context)
    {
        var args = context.children.OfType<LangParser.ExprContext>().Select(Visit);
        return new FuncExpr
        {
            Span = Span(context),
            Name = context.children[0].GetText(),
            Arguments = args.ToArray(),
            Kind = FuncExprKind.Default
        };
    }

    public override Expr VisitBoolean(LangParser.BooleanContext context)
    {
        return new BooleanLiteral(bool.Parse(context.GetText()))
        {
            Span = Span(context)
        };
    }

    public override Expr VisitNull(LangParser.NullContext context)
    {
        return new NullLiteral
        {
            Span = Span(context)
        };
    }

    private static ExprSpan Span(ParserRuleContext context)
    {
        return new ExprSpan(
            (uint)context.Start.Line,
            (uint)context.Start.Column,
            (uint)context.Stop.Line,
            (uint)(context.Stop.Column + context.Stop.Text.Length));
    }

    [GeneratedRegex(@"\\\]")]
    private static partial Regex EscapeRegex();

    private static string UnescapeName(string name)
    {
        if (name[0] == '[' && name[^1] == ']')
        {
            name = name[1..^1];
        }

        return EscapeRegex().Replace(name, "]");
    }
}

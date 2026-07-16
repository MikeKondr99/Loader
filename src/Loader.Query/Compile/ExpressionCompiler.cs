using System.Text;
using Loader.Query.Models;
using Loader.Query.Template;

namespace Loader.Query.Compile;

/// <summary>
/// Минимальный compiler из ReData: проходит по template tokens и рекурсивно раскрывает ArgToken.
/// </summary>
public sealed class ExpressionCompiler : IExpressionCompiler
{
    public string Compile(ResolvedExpression expression)
    {
        var builder = new StringBuilder();
        Compile(builder, expression);
        return builder.ToString();
    }

    public StringBuilder Compile(StringBuilder builder, ResolvedExpression expression)
    {
        foreach (var token in expression.Template.Tokens)
        {
            switch (token)
            {
                case ConstToken constToken:
                    builder.Append(constToken.Text);
                    break;
                case ArgToken argToken:
                    Compile(builder, expression.Arguments[argToken.Index]);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(token), token, null);
            }
        }

        return builder;
    }
}

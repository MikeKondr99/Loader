using System.Text;
using Loader.Query.Models;

namespace Loader.Query.Compile;

/// <summary>
/// Компилирует resolved expression template в SQL-текст.
/// </summary>
public interface IExpressionCompiler
{
    StringBuilder Compile(StringBuilder builder, ResolvedExpression expression);

    string Compile(ResolvedExpression expression)
    {
        var builder = new StringBuilder();
        Compile(builder, expression);
        return builder.ToString();
    }
}
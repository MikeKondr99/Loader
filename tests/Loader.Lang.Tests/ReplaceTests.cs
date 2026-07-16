using Loader.Lang.Expressions;

namespace Loader.Lang.Tests;

public sealed class ReplaceTests
{
    [Test]
    [Arguments("Func(x)  ", "x", "y", "Func(y)")]
    public async Task ReplaceTest(string expr, string pattern, string value, string expected)
    {
        var expr1 = Parse(expr);
        var pattern1 = Parse(pattern);
        var value1 = Parse(value);
        var expected1 = Parse(expected);

        var answer = expr1.Replace(pattern1, value1);
        await Assert.That(answer.Equivalent(expected1)).IsTrue();
    }

    private static Expr Parse(string text)
    {
        var result = Expr.Parse(text);
        return result.Value;
    }
}

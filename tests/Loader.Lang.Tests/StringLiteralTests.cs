using Loader.Lang.Expressions;

namespace Loader.Lang.Tests;

public sealed class StringLiteralTests
{
    [Test]
    [Arguments("'${Name}:${10}'", "Text(Name) + ':' + Text(10)")]
    [Arguments("'${a}1${b}2'", "Text(a) + '1' + Text(b) + '2'")]
    [Arguments("'Hello${Name}'", "'Hello' + Text(Name)")]
    [Arguments("'${Val}%'", "Text(Val) + '%'")]
    [Arguments("'${X}+${Y}=${Sum}'", "Text(X) + '+' + Text(Y) + '=' + Text(Sum)")]
    [Arguments("'Count: ${Count}'", "'Count: ' + Text(Count)")]
    [Arguments("'${A}${B}${C}'", "Text(A) + Text(B) + Text(C)")]
    [Arguments("'1${2}3${4}5'", "'1' + Text(2) + '3' + Text(4) + '5'")]
    [Arguments("'${Start}...${End}'", "Text(Start) + '...' + Text(End)")]
    [Arguments("'${Total} items'", "Text(Total) + ' items'")]
    [Arguments("'${10 + 20} items'", "Text(10 + 20) + ' items'")]
    public async Task Interpolation(string expr, string expected)
    {
        var input = Parse(expr);
        var expect = Parse(expected);
        await Assert.That(input.Equivalent(expect)).IsTrue();
    }

    [Test]
    [Arguments("''", "")]
    [Arguments("'text'", "text")]
    [Arguments("'my string  '", "my string  ")]
    [Arguments(@"'tab\''", @"tab'")]
    [Arguments(@"'(\\)'", @"(\)")]
    [Arguments(@"'(\t)'", "(\t)")]
    [Arguments(@"'(\r)'", "(\r)")]
    [Arguments(@"'(\n)'", "(\n)")]
    [Arguments(@"'(\c)'", @"(\c)")]
    [Arguments(@"'(\{age})'", @"(\{age})")]
    public async Task StringLiteral(string expr, string expected)
    {
        var e = Parse(expr);
        await Assert.That(e.Equivalent(new StringLiteral(expected))).IsTrue();
    }

    private static Expr Parse(string text)
    {
        var result = Expr.Parse(text);
        return result.Value;
    }
}

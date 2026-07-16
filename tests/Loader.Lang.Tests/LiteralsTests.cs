using Loader.Lang.Expressions;

namespace Loader.Lang.Tests;

public sealed class LiteralsTests
{
    [Test]
    [Arguments("name", "name")]
    [Arguments("[first name]", "first name")]
    [Arguments("[ first name  ]", " first name  ")]
    [Arguments(@"[arr[i\]]", "arr[i]")]
    [Arguments("[*?carl$$]", "*?carl$$")]
    [Arguments(@"[\]", @"\")]
    [Arguments(@"[name\]", @"name\")]
    [Arguments("[\"Quote\" me]", "\"Quote\" me")]
    [Arguments("[null]", "null")]
    [Arguments("[true]", "true")]
    [Arguments("[false]", "false")]
    [Arguments("[and]", "and")]
    public async Task NameLiteral(string expr, string expected)
    {
        var e = Parse(expr);
        await Assert.That(e.Equivalent(new NameExpr(expected))).IsTrue();
    }

    [Test]
    [Arguments("''", "")]
    [Arguments("'text'", "text")]
    [Arguments("'my string  '", "my string  ")]
    [Arguments(@"'tab\''", @"tab'")]
    public async Task StringLiteral(string expr, string expected)
    {
        var e = Parse(expr);
        await Assert.That(e.Equivalent(new StringLiteral(expected))).IsTrue();
    }

    [Test]
    [Arguments("1.3", 1.3)]
    [Arguments("0.0", 0.0)]
    [Arguments(".3", .3)]
    [Arguments("5.0", 5.0)]
    [Arguments("0.0000000000", 0.0)]
    [Arguments("0.1234567890", 0.1234567890)]
    public async Task NumberLiteral(string input, double expected)
    {
        var expr = Parse(input);
        await Assert.That(expr.Equivalent(new NumberLiteral(expected))).IsTrue();
    }

    [Test]
    public async Task ShouldParseUnary()
    {
        var expr = Parse("-1");

        await Assert.That(expr.Equivalent(new FuncExpr
        {
            Name = "-",
            Arguments = [new IntegerLiteral(1)],
            Kind = FuncExprKind.Unary
        })).IsTrue();
    }

    [Test]
    [Arguments("0", 0L)]
    [Arguments("10", 10L)]
    [Arguments("123", 123L)]
    [Arguments("4567", 4567L)]
    [Arguments("9999", 9999L)]
    [Arguments("5678", 5678L)]
    [Arguments("00001", 1L)]
    public async Task IntegerLiteral(string input, long expected)
    {
        var expr = Parse(input);
        await Assert.That(expr.Equivalent(new IntegerLiteral(expected))).IsTrue();
    }

    [Test]
    [Arguments("true", true)]
    [Arguments("false", false)]
    public async Task BooleanLiteral(string input, bool expected)
    {
        var expr = Parse(input);
        await Assert.That(expr.Equivalent(new BooleanLiteral(expected))).IsTrue();
    }

    [Test]
    [Arguments("null")]
    public async Task NullLiteral(string input)
    {
        var expr = Parse(input);
        await Assert.That(expr.Equivalent(new NullLiteral())).IsTrue();
    }

    [Test]
    [Arguments("#")]
    [Arguments("a % 3")]
    public async Task ShouldThrowUnexpectedToken(string input)
    {
        var result = Expr.Parse(input);
        await Assert.That(result.IsSuccess).IsFalse();
    }

    private static Expr Parse(string text)
    {
        var result = Expr.Parse(text);
        return result.Value;
    }
}

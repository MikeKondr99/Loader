using Loader.Lang.Expressions;

namespace Loader.Lang.Tests;

public sealed class LiteralsTests
{
    [Test]
    [Arguments("name", "name")]
    [Arguments("_10", "_10")]
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
    [Arguments("[where]", "where")]
    [Arguments("[limit]", "limit")]
    public async Task NameLiteral(string expr, string expected)
    {
        var e = Parse(expr);
        await Assert.That(e.Equivalent(new NameExpr(expected))).IsTrue();
    }

    [Test]
    [Arguments("load")]
    [Arguments("as")]
    [Arguments("from")]
    [Arguments("where")]
    [Arguments("group")]
    [Arguments("order")]
    [Arguments("by")]
    [Arguments("asc")]
    [Arguments("desc")]
    [Arguments("limit")]
    [Arguments("offset")]
    [DisplayName("Expression keyword без квадратных скобок не парсится как имя")]
    public async Task Keyword_is_not_name_literal_without_blocked_name(string input)
    {
        var result = Expr.Parse(input);

        await Assert.That(result.IsSuccess).IsFalse();
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
    [Arguments("1_000.25", 1000.25)]
    [Arguments("1__000.25", 1000.25)]
    [Arguments("1_000_000.125_5", 1000000.1255)]
    [Arguments(".123_456", .123456)]
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
    [Arguments("1_000", 1000L)]
    [Arguments("1__000", 1000L)]
    [Arguments("1_000_000", 1000000L)]
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
    [Arguments("1_")]
    [Arguments("1_.0")]
    [Arguments("1._0")]
    [Arguments("1.")]
    public async Task ShouldThrowUnexpectedToken(string input)
    {
        var result = Expr.Parse(input);
        await Assert.That(result.IsSuccess).IsFalse();
    }

    [Test]
    [Arguments("1_")]
    [Arguments("1_.0")]
    [Arguments("1._0")]
    [DisplayName("Некорректный числовой литерал возвращает понятную ошибку")]
    public async Task Invalid_numeric_literal_returns_domain_error(string input)
    {
        var result = Expr.Parse(input);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error.Message).Contains("Некорректный числовой литерал");
    }

    private static Expr Parse(string text)
    {
        var result = Expr.Parse(text);
        return result.Value;
    }
}

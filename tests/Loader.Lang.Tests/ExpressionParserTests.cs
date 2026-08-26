using Loader.Lang.Expressions;

namespace Loader.Lang.Tests;

public sealed class ExpressionParserTests
{
    [Test]
    [DisplayName("Expression parser разбирает бинарное условие")]
    public async Task Parses_binary_condition()
    {
        var result = Expr.Parse("amount > 0");

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.ToString()).IsEqualTo("([amount] > 0)");
    }

    [Test]
    [DisplayName("Expression parser parses OR as binary operator")]
    public async Task Parses_or_as_binary_operator()
    {
        var result = Expr.Parse("active OR archived");

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsTypeOf<FuncExpr>();
        var expression = (FuncExpr)result.Value;
        await Assert.That(expression.Kind).IsEqualTo(FuncExprKind.Binary);
        await Assert.That(expression.Name).IsEqualTo("or");
    }

    [Test]
    [DisplayName("Expression parser parses AND as binary operator")]
    public async Task Parses_and_as_binary_operator()
    {
        var result = Expr.Parse("active AND archived");

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsTypeOf<FuncExpr>();
        var expression = (FuncExpr)result.Value;
        await Assert.That(expression.Kind).IsEqualTo(FuncExprKind.Binary);
        await Assert.That(expression.Name).IsEqualTo("and");
    }

    [Test]
    [DisplayName("Expression parser разбирает method call")]
    public async Task Parses_method_call()
    {
        var result = Expr.Parse("city.Lower() = 'moscow'");

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.ToString()).IsEqualTo("([city].Lower() = 'moscow')");
    }

    [Test]
    [DisplayName("Expression parser разбирает string interpolation")]
    public async Task Parses_string_interpolation()
    {
        var result = Expr.Parse("'hello ${name}'");

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.ToString()).IsEqualTo("('hello ' + Text([name]))");
    }

    [Test]
    [DisplayName("Expression parser возвращает ошибку парсинга")]
    public async Task Returns_parse_error()
    {
        var result = Expr.Parse("amount >");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error.Message).IsNotNull();
    }
}

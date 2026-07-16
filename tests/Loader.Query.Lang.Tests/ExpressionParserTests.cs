using Loader.Query.Lang.Expressions;

namespace Loader.Query.Lang.Tests;

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

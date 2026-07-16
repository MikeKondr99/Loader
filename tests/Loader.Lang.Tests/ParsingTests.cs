using Loader.Lang.Expressions;

namespace Loader.Lang.Tests;

public sealed class ParsingTests
{
    [Test]
    [DisplayName("Парсер должен строить бинарное выражение для сложения имени и константы")]
    public async Task BinaryOp()
    {
        var expr = Parse("number + 3");

        await Assert.That(expr.Equivalent(new FuncExpr
        {
            Name = "+",
            Kind = FuncExprKind.Binary,
            Arguments =
            [
                new NameExpr("number"),
                new IntegerLiteral(3)
            ]
        })).IsTrue();
    }

    [Test]
    [DisplayName("Парсер должен учитывать приоритет операций умножения над сложением")]
    public async Task ShouldGivePriority()
    {
        var expr = Parse("a + b * c");

        await Assert.That(expr.Equivalent(new FuncExpr
        {
            Name = "+",
            Kind = FuncExprKind.Binary,
            Arguments =
            [
                new NameExpr("a"),
                new FuncExpr
                {
                    Name = "*",
                    Kind = FuncExprKind.Binary,
                    Arguments =
                    [
                        new NameExpr("b"),
                        new NameExpr("c")
                    ]
                }
            ]
        })).IsTrue();
    }

    [Test]
    [DisplayName("Парсер не должен захватывать бинарный оператор внутрь объектного вызова")]
    public async Task ShouldParseWithoutCapturingBinary()
    {
        var expr = Parse("a + c.Call()");

        await Assert.That(expr.Equivalent(new FuncExpr
        {
            Name = "+",
            Kind = FuncExprKind.Binary,
            Arguments =
            [
                new NameExpr("a"),
                new FuncExpr
                {
                    Name = "Call",
                    Kind = FuncExprKind.Method,
                    Arguments = [new NameExpr("c")]
                }
            ]
        })).IsTrue();
    }

    [Test]
    [DisplayName("Парсер должен корректно разбирать строковые литералы в бинарном выражении")]
    public async Task ShouldParseStringNonGreedy()
    {
        var expr = Parse("'a' + 'b'");

        await Assert.That(expr.Equivalent(new FuncExpr
        {
            Name = "+",
            Kind = FuncExprKind.Binary,
            Arguments =
            [
                new StringLiteral("a"),
                new StringLiteral("b")
            ]
        })).IsTrue();
    }

    [Test]
    [DisplayName("Парсер должен корректно разбирать blocked-name в бинарном выражении")]
    public async Task ShouldParseNameNonGreedy()
    {
        var expr = Parse("[a] + [b]");

        await Assert.That(expr.Equivalent(new FuncExpr
        {
            Name = "+",
            Kind = FuncExprKind.Binary,
            Arguments =
            [
                new NameExpr("a"),
                new NameExpr("b")
            ]
        })).IsTrue();
    }

    [Test]
    [DisplayName("Парсер должен корректно разбирать строки внутри аргументов функции")]
    public async Task ShouldParseStringNonGreedyInArguments()
    {
        var expr = Parse("If(10 > 5 and null, 'then', 'else')");

        await Assert.That(expr.Equivalent(new FuncExpr
        {
            Name = "If",
            Arguments =
            [
                new FuncExpr
                {
                    Name = "and",
                    Kind = FuncExprKind.Binary,
                    Arguments =
                    [
                        new FuncExpr
                        {
                            Name = ">",
                            Kind = FuncExprKind.Binary,
                            Arguments =
                            [
                                new IntegerLiteral(10),
                                new IntegerLiteral(5)
                            ]
                        },
                        new NullLiteral()
                    ]
                },
                new StringLiteral("then"),
                new StringLiteral("else")
            ]
        })).IsTrue();
    }

    private static Expr Parse(string text)
    {
        var result = Expr.Parse(text);
        return result.Value;
    }
}

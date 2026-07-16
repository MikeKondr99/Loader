using Loader.Lang.Expressions;
using Loader.Query.Models;
using Loader.Query.Resolve;
using Loader.Query.Template;
using QueryTemplate = Loader.Query.Template.Template;

namespace Loader.Query.Tests;

public sealed class ExpressionResolverTests
{
    [Test]
    [DisplayName("ExpressionResolver резолвит поле в field template")]
    public async Task Resolves_field_to_template()
    {
        var context = CreateContext([]);
        var expression = Expr.Parse("amount").Value;

        var resolved = new ExpressionResolver().Resolve(expression, context);

        await Assert.That(resolved).IsNotNull();
        await Assert.That(resolved!.Template.ToString()).IsEqualTo("stage.amount");
        await Assert.That(resolved.Type.DataType).IsEqualTo(DataType.Number);
    }

    [Test]
    [DisplayName("ExpressionResolver резолвит string literal в SQL literal")]
    public async Task Resolves_string_literal_to_sql_literal()
    {
        var context = CreateContext([]);
        var expression = Expr.Parse("'Moscow'").Value;

        var resolved = new ExpressionResolver().Resolve(expression, context);

        await Assert.That(resolved).IsNotNull();
        await Assert.That(resolved!.Template.ToString()).IsEqualTo("'Moscow'");
        await Assert.That(resolved.Type.IsLiteral).IsTrue();
    }

    [Test]
    [DisplayName("ExpressionResolver резолвит функцию через FunctionResolver")]
    public async Task Resolves_function_with_template_arguments()
    {
        var context = CreateContext(
        [
            new FunctionDefinition
            {
                Name = "Lower",
                Kind = FuncExprKind.Method,
                ArgumentTypes = [DataType.Text],
                ReturnType = DataType.Text,
                Template = QueryTemplate.FromTokens(
                [
                    new ConstToken("lower("),
                    new ArgToken(0),
                    new ConstToken(")")
                ])
            }
        ]);
        var expression = Expr.Parse("city.Lower()").Value;

        var resolved = new ExpressionResolver().Resolve(expression, context);

        await Assert.That(resolved).IsNotNull();
        await Assert.That(resolved!.Template.ToString()).IsEqualTo("lower({0})");
        await Assert.That(resolved.Arguments.Count).IsEqualTo(1);
        await Assert.That(resolved.Arguments[0].Template.ToString()).IsEqualTo("stage.city");
    }

    [Test]
    [DisplayName("ExpressionResolver пишет ошибку если поле не найдено")]
    public async Task Adds_error_when_field_is_missing()
    {
        var context = CreateContext([]);
        var expression = Expr.Parse("missing").Value;

        var resolved = new ExpressionResolver().Resolve(expression, context);

        await Assert.That(resolved).IsNull();
        await Assert.That(context.Errors.Count).IsEqualTo(1);
        await Assert.That(context.Errors[0].Message).IsEqualTo("Поле 'missing' не найдено");
    }

    private static ResolutionContext CreateContext(IReadOnlyList<FunctionDefinition> functions)
    {
        return new ResolutionContext
        {
            Source = new QuerySource
            {
                Name = "stage",
                Fields =
                [
                    new Field
                    {
                        Name = "amount",
                        Type = new FieldType
                        {
                            DataType = DataType.Number,
                            CanBeNull = false
                        }
                    },
                    new Field
                    {
                        Name = "city",
                        Type = new FieldType
                        {
                            DataType = DataType.Text,
                            CanBeNull = true
                        }
                    }
                ]
            },
            Functions = new InMemoryFunctionResolver(functions),
            Errors = []
        };
    }
}

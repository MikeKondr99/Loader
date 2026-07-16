using Loader.Lang.Expressions;
using Loader.Query.Models;
using Loader.Query.Resolve;
using QueryTemplate = Loader.Query.Template.Template;

namespace Loader.Query.Tests;

public sealed class QueryResolverTests
{
    [Test]
    [DisplayName("QueryResolver резолвит select where order и output fields")]
    public async Task Resolves_query_sections()
    {
        var source = new QuerySource
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
                }
            ]
        };
        var query = new Query.Models.Query
        {
            Source = source,
            Select =
            [
                new SelectItem
                {
                    Alias = "amount",
                    Expression = Expr.Parse("amount").Value
                }
            ],
            Where = Expr.Parse("amount > 0").Value,
            OrderBy =
            [
                new OrderItem
                {
                    Expression = Expr.Parse("amount").Value,
                    Direction = OrderDirection.Desc
                }
            ]
        };
        var functions = new InMemoryFunctionResolver(
        [
            new FunctionDefinition
            {
                Name = ">",
                Kind = FuncExprKind.Binary,
                ArgumentTypes = [DataType.Number, DataType.Integer],
                ReturnType = DataType.Boolean,
                Template = QueryTemplate.FromTokens(
                [
                    new Loader.Query.Template.ArgToken(0),
                    new Loader.Query.Template.ConstToken(" > "),
                    new Loader.Query.Template.ArgToken(1)
                ])
            }
        ]);

        var result = new QueryResolver().Resolve(query, functions);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Source).IsSameReferenceAs(source);
        await Assert.That(result.Value.OutputFields[0].Name).IsEqualTo("amount");
        await Assert.That(result.Value.Where!.Template.ToString()).IsEqualTo("{0} > {1}");
        await Assert.That(result.Value.OrderBy[0].Direction).IsEqualTo(OrderDirection.Desc);
    }
}

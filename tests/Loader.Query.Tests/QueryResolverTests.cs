using Loader.Lang.Expressions;
using Loader.Query.Functions;
using Loader.Query.Models;
using Loader.Query.Resolve;
using QueryTemplate = Loader.Query.Template.Template;

namespace Loader.Query.Tests;

public sealed class QueryResolverTests
{
    [Test]
    [DisplayName("QueryResolver резолвит select, where, order и output fields")]
    public async Task Resolves_query_sections()
    {
        var source = new QuerySource
        {
            Sql = "stage",
            Alias = "stage",
            Fields =
            [
                new Field
                {
                    Alias = "amount",
                    Template = QueryTemplate.Text("stage.column1"),
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
        var functions = ClickHouseFunctions.CreateResolver();

        var result = new QueryResolver().Resolve(query, functions);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Source).IsSameReferenceAs(source);
        await Assert.That(result.Value.OutputFields[0].Alias).IsEqualTo("amount");
        await Assert.That(result.Value.Select[0].Alias).IsEqualTo("amount");
        await Assert.That(result.Value.Select[0].ColumnName).IsEqualTo("column1");
        await Assert.That(result.Value.Select[0].Expression.Template.ToString()).IsEqualTo("stage.column1");
        await Assert.That(result.Value.Where!.Template.ToString()).IsEqualTo("({0} > {1})");
        await Assert.That(result.Value.OrderBy[0].Direction).IsEqualTo(OrderDirection.Desc);
    }

    [Test]
    [DisplayName("QueryResolver для SELECT * использует source fields как output fields")]
    public async Task Select_all_uses_source_fields_as_output_fields()
    {
        var source = new QuerySource
        {
            Sql = "stage",
            Alias = "stage",
            Fields =
            [
                new Field
                {
                    Alias = "amount",
                    Template = QueryTemplate.Text("stage.column1"),
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
            Select = []
        };
        var functions = ClickHouseFunctions.CreateResolver();

        var result = new QueryResolver().Resolve(query, functions);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.OutputFields).IsSameReferenceAs(source.Fields);
        await Assert.That(result.Value.OutputFields[0].Alias).IsEqualTo("amount");
    }

    [Test]
    [DisplayName("QueryResolver запрещает LIMIT 0 до компиляции SQL")]
    public async Task Limit_zero_is_rejected()
    {
        var query = new Query.Models.Query
        {
            Source = CreateAmountSource(),
            Select = [],
            Limit = 0
        };
        var functions = ClickHouseFunctions.CreateResolver();

        var result = new QueryResolver().Resolve(query, functions);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Errors.Select(static error => error.Message).ToArray())
            .Contains("LIMIT 0 запрещен. Укажите положительный LIMIT или уберите LIMIT.");
    }

    [Test]
    [DisplayName("QueryResolver запрещает агрегатные выражения в WHERE")]
    public async Task Aggregate_in_where_is_rejected()
    {
        var query = new Query.Models.Query
        {
            Source = CreateCityAmountSource(),
            Select = ["city".As("city")],
            Where = Expr.Parse("SUM(amount) > 0").Value,
            GroupBy = [Expr.Parse("city").Value]
        };
        var functions = ClickHouseFunctions.CreateResolver();

        var result = new QueryResolver().Resolve(query, functions);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Errors.Select(static error => error.Message).ToArray())
            .Contains("WHERE не может содержать агрегатные выражения.");
    }

    [Test]
    [DisplayName("QueryResolver валидирует агрегатный ORDER BY вместе с SELECT")]
    public async Task Aggregate_in_order_by_requires_grouped_select_expressions()
    {
        var query = new Query.Models.Query
        {
            Source = CreateCityAmountSource(),
            Select = ["city".As("city")],
            OrderBy = ["SUM(amount)".Desc()]
        };
        var functions = ClickHouseFunctions.CreateResolver();

        var result = new QueryResolver().Resolve(query, functions);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Errors.Select(static error => error.Message).ToArray())
            .Contains("SELECT expression 'city' должен быть агрегирован или вынесен в GROUP BY.");
    }

    [Test]
    [DisplayName("QueryResolver возвращает несколько ошибок resolve отдельно")]
    public async Task Resolve_returns_multiple_errors()
    {
        var query = new Query.Models.Query
        {
            Source = CreateAmountSource(),
            Select =
            [
                new SelectItem
                {
                    Alias = "bad",
                    Expression = Expr.Parse("missing").Value
                }
            ],
            Limit = 0
        };
        var functions = ClickHouseFunctions.CreateResolver();

        var result = new QueryResolver().Resolve(query, functions);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Errors).Count().IsEqualTo(2);
        await Assert.That(result.Errors.Select(static error => error.Message).ToArray())
            .Contains("LIMIT 0 запрещен. Укажите положительный LIMIT или уберите LIMIT.");
    }

    private static QuerySource CreateAmountSource()
    {
        return new QuerySource
        {
            Sql = "stage",
            Alias = "stage",
            Fields =
            [
                new Field
                {
                    Alias = "amount",
                    Template = QueryTemplate.Text("stage.column1"),
                    Type = new FieldType
                    {
                        DataType = DataType.Number,
                        CanBeNull = false
                    }
                }
            ]
        };
    }

    private static QuerySource CreateCityAmountSource()
    {
        return new QuerySource
        {
            Sql = "stage",
            Alias = "stage",
            Fields =
            [
                new Field
                {
                    Alias = "city",
                    Template = QueryTemplate.Text("stage.column1"),
                    Type = new FieldType
                    {
                        DataType = DataType.Text,
                        CanBeNull = false
                    }
                },
                new Field
                {
                    Alias = "amount",
                    Template = QueryTemplate.Text("stage.column2"),
                    Type = new FieldType
                    {
                        DataType = DataType.Number,
                        CanBeNull = false
                    }
                }
            ]
        };
    }
}

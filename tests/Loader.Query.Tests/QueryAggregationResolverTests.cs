using Loader.Lang.Expressions;
using Loader.Query.Functions;
using Loader.Query.Models;
using Loader.Query.Resolve;
using Loader.Query.Tests.Infrastructure;

namespace Loader.Query.Tests;

public sealed class QueryAggregationResolverTests
{
    [Test]
    [DisplayName("QueryResolver отклоняет вложенную агрегацию")]
    public async Task Rejects_nested_aggregate()
    {
        var query = CreateSingleColumnQuery("AVG(AVG(x))");

        var result = Resolve(query);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Errors.Select(static error => error.Message).ToArray())
            .Contains("Агрегатная функция 'AVG' не может принимать агрегатное выражение.");
    }

    [Test]
    [DisplayName("QueryResolver отклоняет агрегацию от SELECT alias с агрегацией")]
    public async Task Rejects_aggregate_select_alias_inside_aggregate()
    {
        var query = new Query.Models.Query
        {
            Source = CreateSource(),
            Select =
            [
                new SelectItem
                {
                    Alias = "avg_load_field",
                    Expression = Expr.Parse("AVG(x)").Value
                },
                new SelectItem
                {
                    Alias = "test",
                    Expression = Expr.Parse("AVG(avg_load_field)").Value
                }
            ]
        };

        var result = Resolve(query);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Errors.Select(static error => error.Message).ToArray())
            .Contains("Агрегатная функция 'AVG' не может принимать агрегатное выражение.");
    }

    [Test]
    [DisplayName("QueryResolver отклоняет функцию с агрегатными и обычными аргументами")]
    public async Task Rejects_function_with_aggregate_and_regular_arguments()
    {
        var query = CreateSingleColumnQuery("SUM(x) + y");

        var result = Resolve(query);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Errors.Select(static error => error.Message).ToArray())
            .Contains("Аргументы функции '+' должны быть либо все агрегатными, нет.");
    }

    private static ResolveResult<ResolvedQuery> Resolve(Query.Models.Query query)
    {
        return new QueryResolver().Resolve(query, ClickHouseFunctions.CreateResolver());
    }

    private static Query.Models.Query CreateSingleColumnQuery(string expression)
    {
        return new Query.Models.Query
        {
            Source = CreateSource(),
            Select =
            [
                new SelectItem
                {
                    Alias = "test",
                    Expression = Expr.Parse(expression).Value
                }
            ]
        };
    }

    private static QuerySource CreateSource()
    {
        return InlineQueryArrange.Source(
            [
                new InlineField("x", DataType.Integer),
                new InlineField("y", DataType.Integer)
            ],
            [
                ["1", "2"]
            ]);
    }
}

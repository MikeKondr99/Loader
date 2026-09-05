using Loader.Lang.Expressions;
using Loader.Query.Functions;
using Loader.Query.Models;
using Loader.Query.Resolve;
using Loader.Query.Tests.Infrastructure;
using TUnit.Assertions.Enums;

namespace Loader.Query.Tests;

public sealed class QuerySelectAliasExecutionTests : ClickHouseExpressionTestBase
{
    public QuerySelectAliasExecutionTests(ClickHouseTestDatabase database)
        : base(database)
    {
    }

    [Test]
    [DisplayName("Query SELECT alias доступен следующему SELECT выражению")]
    public async Task Select_alias_is_available_to_next_select_expression()
    {
        var query = new Query.Models.Query
        {
            Source = AmountsSource(),
            Select =
            [
                "amount * 2".As("double_amount"),
                "double_amount + 1".As("next_amount")
            ],
            OrderBy = ["id".Asc()]
        };

        var rows = await GetRowsAsync(query);

        await Assert.That(rows.Numbers("double_amount"))
            .IsEquivalentTo([20.0, 40.0, 60.0], CollectionOrdering.Matching);
        await Assert.That(rows.Numbers("next_amount"))
            .IsEquivalentTo([21.0, 41.0, 61.0], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query SELECT alias доступен цепочкой нескольким SELECT выражениям")]
    public async Task Select_alias_chain_is_available_to_later_select_expressions()
    {
        var query = new Query.Models.Query
        {
            Source = AmountsSource(),
            Select =
            [
                "amount + 1".As("a"),
                "a + 1".As("b"),
                "b + a".As("c")
            ],
            OrderBy = ["id".Asc()]
        };

        var rows = await GetRowsAsync(query);

        await Assert.That(rows.Numbers("a"))
            .IsEquivalentTo([11.0, 21.0, 31.0], CollectionOrdering.Matching);
        await Assert.That(rows.Numbers("b"))
            .IsEquivalentTo([12.0, 22.0, 32.0], CollectionOrdering.Matching);
        await Assert.That(rows.Numbers("c"))
            .IsEquivalentTo([23.0, 43.0, 63.0], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query SELECT alias не доступен предыдущему SELECT выражению")]
    public async Task Select_alias_is_not_available_to_previous_select_expression()
    {
        var query = new Query.Models.Query
        {
            Source = AmountsSource(),
            Select =
            [
                "later + 1".As("value"),
                "amount".As("later")
            ]
        };

        var result = Resolve(query);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Errors.Select(static error => error.Message).ToArray())
            .Contains("Поле 'later' не найдено");
    }

    [Test]
    [DisplayName("Query SELECT alias заменяет source field для следующих SELECT выражений")]
    public async Task Select_alias_shadows_source_field_for_later_select_expressions()
    {
        var query = new Query.Models.Query
        {
            Source = AmountsSource(),
            Select =
            [
                "amount * 2".As("amount"),
                "amount + 1".As("next_amount")
            ],
            OrderBy = ["id".Asc()]
        };

        var rows = await GetRowsAsync(query);

        await Assert.That(rows.Numbers("amount"))
            .IsEquivalentTo([20.0, 40.0, 60.0], CollectionOrdering.Matching);
        await Assert.That(rows.Numbers("next_amount"))
            .IsEquivalentTo([21.0, 41.0, 61.0], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query WHERE видит SELECT alias")]
    public async Task Where_can_use_select_alias()
    {
        var query = new Query.Models.Query
        {
            Source = AmountsSource(),
            Select = ["amount * 2".As("double_amount")],
            Where = Expr("double_amount > 30"),
            OrderBy = ["id".Asc()]
        };

        var rows = await GetRowsAsync(query);

        await Assert.That(rows.Numbers("double_amount"))
            .IsEquivalentTo([40.0, 60.0], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query WHERE видит SELECT alias объявленный позже")]
    public async Task Where_can_use_later_select_alias()
    {
        var query = new Query.Models.Query
        {
            Source = AmountsSource(),
            Select =
            [
                "id".As("id"),
                "amount * 2".As("double_amount")
            ],
            Where = Expr("double_amount > 30"),
            OrderBy = ["id".Asc()]
        };

        var rows = await GetRowsAsync(query);

        await Assert.That(rows.Ints("id"))
            .IsEquivalentTo([2, 3], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query WHERE использует SELECT alias вместо одноименного source field")]
    public async Task Where_select_alias_shadows_source_field()
    {
        var query = new Query.Models.Query
        {
            Source = AmountsSource(),
            Select =
            [
                "id".As("id"),
                "amount * 2".As("amount")
            ],
            Where = Expr("amount > 30"),
            OrderBy = ["id".Asc()]
        };

        var rows = await GetRowsAsync(query);

        await Assert.That(rows.Ints("id"))
            .IsEquivalentTo([2, 3], CollectionOrdering.Matching);
        await Assert.That(rows.Numbers("amount"))
            .IsEquivalentTo([40.0, 60.0], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query ORDER BY видит SELECT alias")]
    public async Task Order_by_can_use_select_alias()
    {
        var query = new Query.Models.Query
        {
            Source = AmountsSource(),
            Select =
            [
                "id".As("id"),
                "-amount".As("sort_amount")
            ],
            OrderBy = ["sort_amount".Asc()]
        };

        var rows = await GetRowsAsync(query);

        await Assert.That(rows.Ints("id"))
            .IsEquivalentTo([3, 2, 1], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query ORDER BY использует SELECT alias вместо одноименного source field")]
    public async Task Order_by_select_alias_shadows_source_field()
    {
        var query = new Query.Models.Query
        {
            Source = AmountsSource(),
            Select =
            [
                "id".As("id"),
                "-amount".As("amount")
            ],
            OrderBy = ["amount".Asc()]
        };

        var rows = await GetRowsAsync(query);

        await Assert.That(rows.Ints("id"))
            .IsEquivalentTo([3, 2, 1], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query ORDER BY видит агрегатный SELECT alias")]
    public async Task Order_by_can_use_aggregate_select_alias()
    {
        var query = new Query.Models.Query
        {
            Source = CategoriesSource(),
            Select =
            [
                "category".As("category"),
                "AVG(amount)".As("avg_amount")
            ],
            GroupBy = [Expr("category")],
            OrderBy = ["avg_amount".Desc()]
        };

        var rows = await GetRowsAsync(query);

        await Assert.That(rows.Texts("category"))
            .IsEquivalentTo(["b", "a"], CollectionOrdering.Matching);
        await Assert.That(rows.Numbers("avg_amount"))
            .IsEquivalentTo([35.0, 15.0], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query ORDER BY видит SELECT alias производный от агрегатного alias")]
    public async Task Order_by_can_use_select_alias_derived_from_aggregate_alias()
    {
        var query = new Query.Models.Query
        {
            Source = CategoriesSource(),
            Select =
            [
                "category".As("category"),
                "AVG(amount)".As("avg_amount"),
                "avg_amount + 1".As("avg_plus_one")
            ],
            GroupBy = [Expr("category")],
            OrderBy = ["avg_plus_one".Desc()]
        };

        var rows = await GetRowsAsync(query);

        await Assert.That(rows.Texts("category"))
            .IsEquivalentTo(["b", "a"], CollectionOrdering.Matching);
        await Assert.That(rows.Numbers("avg_plus_one"))
            .IsEquivalentTo([36.0, 16.0], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query агрегатная функция не принимает агрегатный SELECT alias")]
    public async Task Aggregate_function_rejects_aggregate_select_alias()
    {
        var query = new Query.Models.Query
        {
            Source = AmountsSource(),
            Select =
            [
                "AVG(amount)".As("avg_amount"),
                "AVG(avg_amount)".As("bad")
            ]
        };

        var result = Resolve(query);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Errors.Select(static error => error.Message).ToArray())
            .Contains("Агрегатная функция 'AVG' не может принимать агрегатное выражение.");
    }

    [Test]
    [DisplayName("Query обычная функция принимает агрегатный SELECT alias")]
    public async Task Regular_function_can_use_aggregate_select_alias()
    {
        var query = new Query.Models.Query
        {
            Source = CategoriesSource(),
            Select =
            [
                "category".As("category"),
                "SUM(amount)".As("total"),
                "Round(total)".As("rounded_total")
            ],
            GroupBy = [Expr("category")],
            OrderBy = ["category".Asc()]
        };

        var rows = await GetRowsAsync(query);

        await Assert.That(rows.Numbers("rounded_total"))
            .IsEquivalentTo([30.0, 70.0], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query GROUP BY видит SELECT alias")]
    public async Task Group_by_can_use_select_alias()
    {
        var query = new Query.Models.Query
        {
            Source = CategoriesSource(),
            Select =
            [
                "Upper(category)".As("category_key"),
                "COUNT()".As("count")
            ],
            GroupBy = [Expr("category_key")],
            OrderBy = ["category_key".Asc()]
        };

        var rows = await GetRowsAsync(query);

        await Assert.That(rows.Texts("category_key"))
            .IsEquivalentTo(["A", "B"], CollectionOrdering.Matching);
        await Assert.That(rows.Ints("count"))
            .IsEquivalentTo([2, 2], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query GROUP BY использует SELECT alias вместо одноименного source field")]
    public async Task Group_by_select_alias_shadows_source_field()
    {
        var query = new Query.Models.Query
        {
            Source = CategoriesSource(),
            Select =
            [
                "Upper(category)".As("category"),
                "COUNT()".As("count")
            ],
            GroupBy = [Expr("category")],
            OrderBy = ["category".Asc()]
        };

        var rows = await GetRowsAsync(query);

        await Assert.That(rows.Texts("category"))
            .IsEquivalentTo(["A", "B"], CollectionOrdering.Matching);
        await Assert.That(rows.Ints("count"))
            .IsEquivalentTo([2, 2], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query ORDER BY одноименный SELECT alias не совпадает с GROUP BY выражением")]
    public async Task Order_by_same_name_select_alias_does_not_match_group_by_expression()
    {
        var query = new Query.Models.Query
        {
            Source = CategoriesSource(),
            Select =
            [
                "Upper(category)".As("category"),
                "COUNT()".As("count")
            ],
            GroupBy = [Expr("Upper(category)")],
            OrderBy = ["category".Asc()]
        };

        var result = Resolve(query);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Errors.Select(static error => error.Message).ToArray())
            .Contains("ORDER BY expression должен быть агрегирован или совпадать с выражением из GROUP BY.");
    }

    [Test]
    [DisplayName("Query ORDER BY выражение от GROUP BY SELECT alias сейчас отклоняется")]
    public async Task Order_by_expression_over_group_by_select_alias_is_rejected()
    {
        var query = new Query.Models.Query
        {
            Source = CategoriesSource(),
            Select =
            [
                "Upper(category)".As("category_key"),
                "COUNT()".As("count")
            ],
            GroupBy = [Expr("category_key")],
            OrderBy = ["Lower(category_key)".Asc()]
        };

        var result = Resolve(query);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Errors.Select(static error => error.Message).ToArray())
            .Contains("ORDER BY expression должен быть агрегирован или совпадать с выражением из GROUP BY.");
    }

    [Test]
    [DisplayName("Query GROUP BY агрегатный SELECT alias отклоняется")]
    public async Task Group_by_rejects_aggregate_select_alias()
    {
        var query = new Query.Models.Query
        {
            Source = CategoriesSource(),
            Select =
            [
                "AVG(amount)".As("avg_amount")
            ],
            GroupBy = [Expr("avg_amount")]
        };

        var result = Resolve(query);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Errors.Select(static error => error.Message).ToArray())
            .Contains("GROUP BY не может содержать агрегатные выражения.");
    }

    [Test]
    [DisplayName("Query duplicate SELECT alias отклоняется")]
    public async Task Duplicate_select_alias_is_rejected()
    {
        var query = new Query.Models.Query
        {
            Source = AmountsSource(),
            Select =
            [
                "amount".As("value"),
                "amount * 2".As("value")
            ]
        };

        var result = Resolve(query);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Errors.Select(static error => error.Message).ToArray())
            .Contains("LOAD select alias 'value' дублируется.");
    }

    [Test]
    [DisplayName("Query SELECT alias чувствителен к регистру")]
    public async Task Select_alias_is_case_sensitive()
    {
        var query = new Query.Models.Query
        {
            Source = AmountsSource(),
            Select =
            [
                "amount".As("Value"),
                "Value + 1".As("next")
            ],
            OrderBy = ["id".Asc()]
        };

        var rows = await GetRowsAsync(query);

        await Assert.That(rows.Numbers("next"))
            .IsEquivalentTo([11.0, 21.0, 31.0], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query SELECT alias с другим регистром не находится")]
    public async Task Select_alias_with_different_case_is_not_found()
    {
        var query = new Query.Models.Query
        {
            Source = AmountsSource(),
            Select =
            [
                "amount".As("Value"),
                "value + 1".As("next")
            ]
        };

        var result = Resolve(query);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Errors.Select(static error => error.Message).ToArray())
            .Contains("Поле 'value' не найдено");
    }

    private static ResolveResult<ResolvedQuery> Resolve(Query.Models.Query query)
    {
        return new QueryResolver().Resolve(query, ClickHouseFunctions.CreateResolver());
    }

    private static Expr Expr(string expression)
    {
        return Loader.Lang.Expressions.Expr.Parse(expression).Value;
    }

    private static QuerySource AmountsSource()
    {
        return InlineQueryArrange.Source(
            [
                new InlineField("id", DataType.Integer),
                new InlineField("amount", DataType.Number)
            ],
            [
                ["1", "10.0"],
                ["2", "20.0"],
                ["3", "30.0"]
            ]);
    }

    private static QuerySource CategoriesSource()
    {
        return InlineQueryArrange.Source(
            [
                new InlineField("category", DataType.Text),
                new InlineField("amount", DataType.Number)
            ],
            [
                ["'a'", "10.0"],
                ["'a'", "20.0"],
                ["'b'", "30.0"],
                ["'b'", "40.0"]
            ]);
    }
}

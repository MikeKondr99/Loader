using System.Globalization;
using Loader.Core.Exceptions;
using Loader.Lang.Expressions;
using Loader.Query.Functions;
using Loader.Query.Models;
using Loader.Query.Resolve;
using Loader.Query.Tests.Infrastructure;
using TUnit.Assertions.Enums;

namespace Loader.Query.Tests;

public sealed class QueryEdgeCaseExecutionTests : ClickHouseExpressionTestBase
{
    public QueryEdgeCaseExecutionTests(ClickHouseTestDatabase database)
        : base(database)
    {
    }

    [Test]
    [DisplayName("Query сохраняет трехзначную логику для nullable bool SELECT выражений")]
    public async Task Nullable_boolean_select_expressions_preserve_null()
    {
        // Arrange
        var query = new Query.Models.Query
        {
            Source = NullableBoolSource(),
            Select =
            [
                "id".As("id"),
                "flag".As("flag"),
                "Not(flag)".As("not_flag")
            ],
            OrderBy = ["id".Asc()]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Ints("id"))
            .IsEquivalentTo([1, 2, 3], CollectionOrdering.Matching);
        await Assert.That(rows.Values("flag"))
            .IsEquivalentTo((object?[])[true, false, null], CollectionOrdering.Matching);
        await Assert.That(rows.Values("not_flag"))
            .IsEquivalentTo((object?[])[false, true, null], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query резолвит ключевое слово OR как бинарный оператор")]
    public async Task Or_keyword_is_resolved_as_binary_operator()
    {
        // Arrange
        var query = new Query.Models.Query
        {
            Source = NullableBoolSource(),
            Select = ["flag OR false".As("value")],
            OrderBy = ["id".Asc()]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Values("value"))
            .IsEquivalentTo((object?[])[true, false, null], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query резолвит ключевое слово AND как бинарный оператор")]
    public async Task And_keyword_is_resolved_as_binary_operator()
    {
        // Arrange
        var query = new Query.Models.Query
        {
            Source = NullableBoolSource(),
            Select = ["flag AND true".As("value")],
            OrderBy = ["id".Asc()]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Values("value"))
            .IsEquivalentTo((object?[])[true, false, null], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query резолвит равенство для bool")]
    public async Task Boolean_equality_is_supported()
    {
        // Arrange
        var query = new Query.Models.Query
        {
            Source = NullableBoolSource(),
            Select = ["flag = false".As("is_false")],
            OrderBy = ["id".Asc()]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Values("is_false"))
            .IsEquivalentTo((object?[])[false, true, null], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query WHERE по nullable bool оставляет только TRUE")]
    public async Task Where_nullable_boolean_keeps_only_true()
    {
        // Arrange
        var query = new Query.Models.Query
        {
            Source = NullableBoolSource(),
            Select = ["id".As("id")],
            Where = Expr("flag"),
            OrderBy = ["id".Asc()]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Ints("id"))
            .IsEquivalentTo([1], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query WHERE Not(nullable bool) оставляет только FALSE")]
    public async Task Where_not_nullable_boolean_keeps_only_false()
    {
        // Arrange
        var query = new Query.Models.Query
        {
            Source = NullableBoolSource(),
            Select = ["id".As("id")],
            Where = Expr("Not(flag)"),
            OrderBy = ["id".Asc()]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Ints("id"))
            .IsEquivalentTo([2], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query WHERE отклоняет не bool выражение на resolve")]
    public async Task Where_non_boolean_expression_is_rejected_by_resolver()
    {
        // Arrange
        var query = new Query.Models.Query
        {
            Source = CityAmountsSource(),
            Select = ["city".As("city")],
            Where = Expr("amount")
        };

        // Act
        var act = async () => await GetRowsAsync(query);

        // Assert
        await Assert.That(act)
            .ThrowsExactly<InvalidOperationException>()
            .WithMessage("WHERE expression должен возвращать Boolean.");
    }

    [Test]
    [DisplayName("Query группирует LowCardinality(String) как обычный Text")]
    public async Task Low_cardinality_text_groups_like_text()
    {
        // Arrange
        var source = InlineQueryArrange.Source(
            [new InlineField("city", DataType.Text)],
            [
                ["CAST('Moscow' AS LowCardinality(String))"],
                ["CAST('Paris' AS LowCardinality(String))"],
                ["CAST('Moscow' AS LowCardinality(String))"],
                ["CAST('Berlin' AS LowCardinality(String))"],
                ["CAST('Moscow' AS LowCardinality(String))"],
                ["CAST('Paris' AS LowCardinality(String))"]
            ]);
        var query = new Query.Models.Query
        {
            Source = source,
            Select =
            [
                "city".As("city"),
                "COUNT()".As("count")
            ],
            GroupBy = [Expr("city")],
            OrderBy =
            [
                "COUNT()".Desc(),
                "city".Asc()
            ]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Texts("city"))
            .IsEquivalentTo(["Moscow", "Paris", "Berlin"], CollectionOrdering.Matching);
        await Assert.That(rows.Ints("count"))
            .IsEquivalentTo([3, 2, 1], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query применяет string function к LowCardinality(String)")]
    public async Task Low_cardinality_text_works_with_string_functions()
    {
        // Arrange
        var source = InlineQueryArrange.Source(
            [new InlineField("city", DataType.Text)],
            [
                ["CAST('Moscow' AS LowCardinality(String))"],
                ["CAST('Paris' AS LowCardinality(String))"]
            ]);
        var query = new Query.Models.Query
        {
            Source = source,
            Select = ["Upper(city)".As("city")],
            OrderBy = ["city".Asc()]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Texts("city"))
            .IsEquivalentTo(["MOSCOW", "PARIS"], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query GROUP BY nullable key создает отдельную NULL группу")]
    public async Task Group_by_nullable_key_creates_null_bucket()
    {
        // Arrange
        var source = InlineQueryArrange.Source(
            [
                new InlineField("city", DataType.Text, CanBeNull: true)
            ],
            [
                ["'Moscow'"],
                ["NULL"],
                ["'Moscow'"],
                ["NULL"]
            ]);
        var query = new Query.Models.Query
        {
            Source = source,
            Select =
            [
                "city".As("city"),
                "city.IsNull()".As("is_null"),
                "COUNT()".As("count")
            ],
            GroupBy =
            [
                Expr("city"),
                Expr("city.IsNull()")
            ],
            OrderBy =
            [
                "city.IsNull()".Asc(),
                "city".Asc()
            ]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Values("city"))
            .IsEquivalentTo((object?[])["Moscow", null], CollectionOrdering.Matching);
        await Assert.That(rows.Values("is_null"))
            .IsEquivalentTo((object?[])[false, true], CollectionOrdering.Matching);
        await Assert.That(rows.Ints("count"))
            .IsEquivalentTo([2, 2], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query сортирует по агрегату которого нет в SELECT")]
    public async Task Order_by_aggregate_does_not_need_to_be_selected()
    {
        // Arrange
        var source = InlineQueryArrange.Source(
            [
                new InlineField("city", DataType.Text),
                new InlineField("amount", DataType.Number)
            ],
            [
                ["'Moscow'", "10.0"],
                ["'Paris'", "100.0"],
                ["'Berlin'", "5.0"],
                ["'Moscow'", "20.0"]
            ]);
        var query = new Query.Models.Query
        {
            Source = source,
            Select = ["city".As("city")],
            GroupBy = [Expr("city")],
            OrderBy =
            [
                "SUM(amount)".Desc(),
                "city".Asc()
            ]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Texts("city"))
            .IsEquivalentTo(["Paris", "Moscow", "Berlin"], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query агрегат в WHERE отклоняет на resolve")]
    public async Task Aggregate_in_where_is_rejected_by_resolver()
    {
        // Arrange
        var source = InlineQueryArrange.Source(
            [
                new InlineField("city", DataType.Text),
                new InlineField("amount", DataType.Number)
            ],
            [
                ["'Moscow'", "10.0"],
                ["'Paris'", "100.0"]
            ]);
        var query = new Query.Models.Query
        {
            Source = source,
            Select = ["city".As("city")],
            Where = Expr("SUM(amount) > 0"),
            GroupBy = [Expr("city")]
        };

        // Act
        var act = async () => await GetRowsAsync(query);

        // Assert
        await Assert.That(act)
            .ThrowsExactly<InvalidOperationException>()
            .WithMessage("WHERE не может содержать агрегатные выражения.");
    }

    [Test]
    [DisplayName("Query агрегат в ORDER BY без GROUP BY валидирует SELECT на resolve")]
    public async Task Aggregate_in_order_by_without_group_by_validates_select_by_resolver()
    {
        // Arrange
        var source = InlineQueryArrange.Source(
            [
                new InlineField("city", DataType.Text),
                new InlineField("amount", DataType.Number)
            ],
            [
                ["'Moscow'", "10.0"],
                ["'Paris'", "100.0"]
            ]);
        var query = new Query.Models.Query
        {
            Source = source,
            Select = ["city".As("city")],
            OrderBy = ["SUM(amount)".Desc()]
        };

        // Act
        var act = async () => await GetRowsAsync(query);

        // Assert
        await Assert.That(act)
            .ThrowsExactly<InvalidOperationException>()
            .WithMessage("SELECT expression 'city' должен быть агрегирован или вынесен в GROUP BY.");
    }

    [Test]
    [DisplayName("Query резолвит COUNT(bool)")]
    public async Task Count_boolean_is_supported()
    {
        // Arrange
        var query = new Query.Models.Query
        {
            Source = NullableBoolSource(),
            Select = ["COUNT(flag)".As("flags")]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows[0].Int("flags")).IsEqualTo(2);
    }

    [Test]
    [DisplayName("Query COUNT() считает строки с nullable bool источником")]
    public async Task Count_all_rows_with_nullable_boolean_source()
    {
        // Arrange
        var query = new Query.Models.Query
        {
            Source = NullableBoolSource(),
            Select = ["COUNT()".As("rows")]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows[0].Int("rows")).IsEqualTo(3);
    }

    [Test]
    [DisplayName("Query позволяет SELECT alias заменить имя поля источника")]
    public async Task Select_alias_can_replace_source_field_name()
    {
        // Arrange
        var source = AmountsSource();
        var query = new Query.Models.Query
        {
            Source = source,
            Select = ["amount * 2".As("amount")],
            OrderBy = ["id".Asc()]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Numbers("amount"))
            .IsEquivalentTo([20.0, 40.0, 60.0], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query резолвит SELECT alias последовательно")]
    public async Task Select_aliases_are_resolved_sequentially()
    {
        // Arrange
        var source = AmountsSource();
        var query = new Query.Models.Query
        {
            Source = source,
            Select =
            [
                "amount * 2".As("amount"),
                "amount + 1".As("next_amount")
            ],
            OrderBy = ["id".Asc()]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Numbers("amount"))
            .IsEquivalentTo([20.0, 40.0, 60.0], CollectionOrdering.Matching);
        await Assert.That(rows.Numbers("next_amount"))
            .IsEquivalentTo([21.0, 41.0, 61.0], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query duplicate SELECT alias отклоняется на resolve")]
    public async Task Duplicate_select_alias_is_rejected()
    {
        // Arrange
        var source = AmountsSource();
        var query = new Query.Models.Query
        {
            Source = source,
            Select =
            [
                "amount".As("value"),
                "amount * 2".As("value")
            ]
        };

        // Act
        var act = async () => await GetRowsAsync(query);

        // Assert
        await Assert.That(act)
            .ThrowsExactly<InvalidOperationException>()
            .WithMessage("LOAD select alias 'value' дублируется.");
    }

    [Test]
    [DisplayName("Query поддерживает ORDER BY по SELECT alias")]
    public async Task Order_by_select_alias_is_supported()
    {
        // Arrange
        var source = AmountsSource();
        var query = new Query.Models.Query
        {
            Source = source,
            Select = ["amount * 2".As("total")],
            OrderBy = ["total".Asc()]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Numbers("total"))
            .IsEquivalentTo([20.0, 40.0, 60.0], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query поддерживает WHERE по SELECT alias")]
    public async Task Where_select_alias_is_supported()
    {
        // Arrange
        var source = AmountsSource();
        var query = new Query.Models.Query
        {
            Source = source,
            Select = ["amount * 2".As("total")],
            Where = Expr("total > 30"),
            OrderBy = ["id".Asc()]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Numbers("total"))
            .IsEquivalentTo([40.0, 60.0], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query поддерживает GROUP BY по SELECT alias")]
    public async Task Group_by_select_alias_is_supported()
    {
        // Arrange
        var source = InlineQueryArrange.Source(
            [new InlineField("city", DataType.Text)],
            [
                ["'Moscow'"],
                ["'moscow'"],
                ["'Paris'"]
            ]);
        var query = new Query.Models.Query
        {
            Source = source,
            Select =
            [
                "Upper(city)".As("city"),
                "COUNT()".As("count")
            ],
            GroupBy = [Expr("city")],
            OrderBy = ["city".Asc()]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Texts("city"))
            .IsEquivalentTo(["MOSCOW", "PARIS"], CollectionOrdering.Matching);
        await Assert.That(rows.Ints("count"))
            .IsEquivalentTo([2, 1], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query группирует по выражению")]
    public async Task Group_by_expression()
    {
        // Arrange
        var source = InlineQueryArrange.Source(
            [new InlineField("city", DataType.Text)],
            [
                ["'Moscow'"],
                ["'moscow'"],
                ["'Paris'"]
            ]);
        var query = new Query.Models.Query
        {
            Source = source,
            Select =
            [
                "Upper(city)".As("city"),
                "COUNT()".As("count")
            ],
            GroupBy = [Expr("Upper(city)")],
            OrderBy = ["Upper(city)".Asc()]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Texts("city"))
            .IsEquivalentTo(["MOSCOW", "PARIS"], CollectionOrdering.Matching);
        await Assert.That(rows.Ints("count"))
            .IsEquivalentTo([2, 1], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query считает function call и method call одинаковыми для GROUP BY ORDER BY")]
    public async Task Group_by_function_call_matches_order_by_method_call()
    {
        // Arrange
        var source = InlineQueryArrange.Source(
            [new InlineField("name", DataType.Text)],
            [
                ["'bob'"],
                ["'Alice'"],
                ["'BOB'"]
            ]);
        var query = new Query.Models.Query
        {
            Source = source,
            Select =
            [
                "Lower(name)".As("name"),
                "COUNT()".As("count")
            ],
            GroupBy = [Expr("Lower(name)")],
            OrderBy = ["name.Lower()".Asc()]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Texts("name"))
            .IsEquivalentTo(["alice", "bob"], CollectionOrdering.Matching);
        await Assert.That(rows.Ints("count"))
            .IsEquivalentTo([1, 2], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query ORDER BY выражение от GROUP BY поля сейчас отклоняется")]
    public async Task Order_by_expression_over_group_by_field_is_rejected()
    {
        // Arrange
        var source = InlineQueryArrange.Source(
            [new InlineField("category", DataType.Text)],
            [
                ["'a'"],
                ["'A'"],
                ["'b'"]
            ]);
        var query = new Query.Models.Query
        {
            Source = source,
            Select =
            [
                "category".As("category"),
                "COUNT()".As("count")
            ],
            GroupBy = [Expr("category")],
            OrderBy = ["Upper(category)".Asc()]
        };

        // Act
        var act = async () => await GetRowsAsync(query);

        // Assert
        await Assert.That(act)
            .ThrowsExactly<InvalidOperationException>()
            .WithMessage("ORDER BY expression должен быть агрегирован или совпадать с выражением из GROUP BY.");
    }

    [Test]
    [DisplayName("Query применяет string functions в WHERE")]
    public async Task Where_with_string_functions()
    {
        // Arrange
        var source = InlineQueryArrange.Source(
            [new InlineField("name", DataType.Text)],
            [
                ["'  Mike  '"],
                ["'Ann'"],
                ["''"],
                ["'Bob'"]
            ]);
        var query = new Query.Models.Query
        {
            Source = source,
            Select = ["Trim(name)".As("name")],
            Where = Expr("Len(Trim(name)) > 3"),
            OrderBy = ["Trim(name)".Asc()]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Texts("name"))
            .IsEquivalentTo(["Mike"], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query фиксирует порядок NULL при ORDER BY ASC и DESC")]
    public async Task Null_ordering_is_stable()
    {
        // Arrange
        var source = NullableNumbersSource();
        var ascQuery = new Query.Models.Query
        {
            Source = source,
            Select = ["id".As("id")],
            OrderBy = ["score".Asc()]
        };
        var descQuery = ascQuery with
        {
            OrderBy = ["score".Desc()]
        };

        // Act
        var ascRows = await GetRowsAsync(ascQuery);
        var descRows = await GetRowsAsync(descQuery);

        // Assert
        await Assert.That(ascRows.Ints("id"))
            .IsEquivalentTo([3, 1, 2, 4], CollectionOrdering.Matching);
        await Assert.That(descRows.Ints("id"))
            .IsEquivalentTo([1, 3, 2, 4], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query OFFSET за пределами строк возвращает пустой набор без ошибки")]
    public async Task Offset_beyond_row_count_returns_empty_rows()
    {
        // Arrange
        var query = new Query.Models.Query
        {
            Source = AmountsSource(),
            Select = [],
            OrderBy = ["id".Asc()],
            Offset = 100
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows).IsEmpty();
    }

    [Test]
    [DisplayName("Query неизвестное поле в WHERE отклоняет на resolve")]
    public async Task Unknown_field_in_where_is_rejected_by_resolver()
    {
        // Arrange
        var query = new Query.Models.Query
        {
            Source = AmountsSource(),
            Select = [],
            Where = Expr("missing > 0")
        };

        // Act
        var act = async () => await GetRowsAsync(query);

        // Assert
        await Assert.That(act).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    [DisplayName("Query неизвестное поле в GROUP BY отклоняет на resolve")]
    public async Task Unknown_field_in_group_by_is_rejected_by_resolver()
    {
        // Arrange
        var query = new Query.Models.Query
        {
            Source = AmountsSource(),
            Select = ["COUNT()".As("count")],
            GroupBy = [Expr("missing")]
        };

        // Act
        var act = async () => await GetRowsAsync(query);

        // Assert
        await Assert.That(act).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    [DisplayName("Query неизвестное поле в ORDER BY отклоняет на resolve")]
    public async Task Unknown_field_in_order_by_is_rejected_by_resolver()
    {
        // Arrange
        var query = new Query.Models.Query
        {
            Source = AmountsSource(),
            Select = [],
            OrderBy = ["missing".Asc()]
        };

        // Act
        var act = async () => await GetRowsAsync(query);

        // Assert
        await Assert.That(act).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    [DisplayName("Query aggregate с non-grouped field отклоняет на resolve")]
    public async Task Aggregate_with_non_grouped_field_is_rejected_by_resolver()
    {
        // Arrange
        var query = new Query.Models.Query
        {
            Source = CityAmountsSource(),
            Select =
            [
                "city".As("city"),
                "SUM(amount)".As("total")
            ]
        };

        // Act
        var act = async () => await GetRowsAsync(query);

        // Assert
        await Assert.That(act)
            .ThrowsExactly<InvalidOperationException>()
            .WithMessage("SELECT expression 'city' должен быть агрегирован или вынесен в GROUP BY.");
    }

    [Test]
    [DisplayName("Query nested aggregation отклоняет на resolve")]
    public async Task Nested_aggregation_is_rejected_by_resolver()
    {
        // Arrange
        var query = new Query.Models.Query
        {
            Source = AmountsSource(),
            Select = ["SUM(COUNT())".As("bad")]
        };

        // Act
        var act = async () => await GetRowsAsync(query);

        // Assert
        await Assert.That(act).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    [DisplayName("Query aggregation в GROUP BY отклоняет на resolve")]
    public async Task Aggregation_in_group_by_is_rejected_by_resolver()
    {
        // Arrange
        var query = new Query.Models.Query
        {
            Source = AmountsSource(),
            Select = ["COUNT()".As("count")],
            GroupBy = [Expr("SUM(amount)")]
        };

        // Act
        var act = async () => await GetRowsAsync(query);

        // Assert
        await Assert.That(act)
            .ThrowsExactly<InvalidOperationException>()
            .WithMessage("GROUP BY не может содержать агрегатные выражения.");
    }

    [Test]
    [DisplayName("Query SELECT star с GROUP BY отклоняет на resolve")]
    public async Task Select_star_with_group_by_is_rejected_by_resolver()
    {
        // Arrange
        var query = new Query.Models.Query
        {
            Source = CityAmountsSource(),
            Select = [],
            GroupBy = [Expr("city")]
        };

        // Act
        var act = async () => await GetRowsAsync(query);

        // Assert
        await Assert.That(act)
            .ThrowsExactly<InvalidOperationException>()
            .WithMessage("SELECT * нельзя использовать вместе с GROUP BY. Перечислите группируемые и агрегированные поля явно.");
    }

    [Test]
    [DisplayName("Query aggregation по пустому результату возвращает expected defaults")]
    public async Task Empty_result_aggregation_returns_expected_defaults()
    {
        // Arrange
        var query = new Query.Models.Query
        {
            Source = AmountsSource(),
            Select =
            [
                "COUNT()".As("count"),
                "SUM(amount)".As("sum"),
                "AVG(amount)".As("avg"),
                "MIN(amount)".As("min"),
                "MAX(amount)".As("max")
            ],
            Where = Expr("false")
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows[0].Int("count")).IsEqualTo(0);
        await AssertNumberAsync(rows[0]["sum"], 0);
        await Assert.That(rows[0]["avg"]).IsNull();
        await Assert.That(rows[0]["min"]).IsNull();
        await Assert.That(rows[0]["max"]).IsNull();
    }

    [Test]
    [DisplayName("Query nullable числовое выражение сохраняет NULL")]
    public async Task Nullable_numeric_expression_preserves_null()
    {
        // Arrange
        var query = new Query.Models.Query
        {
            Source = NullableNumbersSource(),
            Select =
            [
                "id".As("id"),
                "score + 1".As("score_plus_one")
            ],
            OrderBy = ["id".Asc()]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Values("score_plus_one"))
            .IsEquivalentTo((object?[])[11.0, null, 8.5, null], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query Alt с числовым выражением не создает ClickHouse Variant")]
    public async Task Numeric_alt_expression_does_not_create_clickhouse_variant()
    {
        // Arrange
        var source = InlineQueryArrange.Source(
            [
                new InlineField("id", DataType.Integer),
                new InlineField("raw", DataType.Text)
            ],
            [
                ["1", "'12.34'"],
                ["2", "''"],
                ["3", "'bad'"]
            ]);
        var query = new Query.Models.Query
        {
            Source = source,
            Select =
            [
                "raw.Num().Alt(0.0)".As("amount"),
                "RawType(raw.Num().Alt(0.0))".As("amount_type")
            ],
            OrderBy = ["id".Asc()]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Numbers("amount"))
            .IsEquivalentTo([12.34, 0.0, 0.0], CollectionOrdering.Matching);
        await Assert.That(rows.Texts("amount_type"))
            .IsEquivalentTo(
                ["Nullable(Decimal(18, 10))", "Nullable(Decimal(18, 10))", "Nullable(Decimal(18, 10))"],
                CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query Alt с числовым выражением приводит decimal источника и литерал к одному scale")]
    public async Task Numeric_alt_expression_unifies_source_decimal_and_literal_scale()
    {
        // Arrange
        var source = InlineQueryArrange.Source(
            [new InlineField("amount", DataType.Number, CanBeNull: true)],
            [
                ["CAST(12.34 AS Nullable(Decimal(10, 2)))"],
                ["CAST(NULL AS Nullable(Decimal(10, 2)))"]
            ]);
        var query = new Query.Models.Query
        {
            Source = source,
            Select =
            [
                "amount.Alt(0.0)".As("amount"),
                "RawType(amount.Alt(0.0))".As("amount_type")
            ],
            OrderBy = ["amount".Asc()]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Numbers("amount").Order().ToArray())
            .IsEquivalentTo([0.0, 12.34], CollectionOrdering.Matching);
        await Assert.That(rows.Texts("amount_type"))
            .IsEquivalentTo(
                ["Nullable(Decimal(18, 10))", "Nullable(Decimal(18, 10))"],
                CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query применяет implicit cast int и num в одном выражении")]
    public async Task Mixed_integer_number_expression_uses_implicit_casts()
    {
        // Arrange
        var source = InlineQueryArrange.Source(
            [
                new InlineField("id", DataType.Integer),
                new InlineField("ratio", DataType.Number)
            ],
            [
                ["1", "0.5"],
                ["2", "1.25"]
            ]);
        var query = new Query.Models.Query
        {
            Source = source,
            Select = ["id + ratio".As("value")],
            OrderBy = ["id".Asc()]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Numbers("value"))
            .IsEquivalentTo([1.5, 3.25], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query SELECT * с дубликатами alias источника сейчас отклоняется слоем reader/schema")]
    public async Task Select_all_with_duplicate_source_aliases_is_rejected()
    {
        // Arrange
        var source = InlineQueryArrange.Source(
            [
                new InlineField("value", DataType.Integer),
                new InlineField("value", DataType.Integer)
            ],
            [
                ["1", "2"]
            ]);
        var query = new Query.Models.Query
        {
            Source = source,
            Select = []
        };

        // Act
        var act = async () => await GetRowsAsync(query);

        // Assert
        await Assert.That(act).Throws<Exception>();
    }

    [Test]
    [DisplayName("Query применяет JsonGet в WHERE GROUP BY ORDER BY")]
    public async Task Json_get_works_in_where_group_by_order_by()
    {
        // Arrange
        var source = InlineQueryArrange.Source(
            [new InlineField("payload", DataType.Text)],
            [
                ["'{\"city\":\"Moscow\",\"keep\":true}'"],
                ["'{\"city\":\"Paris\",\"keep\":true}'"],
                ["'{\"city\":\"Moscow\",\"keep\":true}'"],
                ["'{\"city\":\"Berlin\",\"keep\":false}'"],
                ["'not-json'"]
            ]);
        var query = new Query.Models.Query
        {
            Source = source,
            Select =
            [
                "payload.JsonGetText('$.city')".As("city"),
                "COUNT()".As("count")
            ],
            Where = Expr("payload.JsonGetBool('$.keep')"),
            GroupBy = [Expr("payload.JsonGetText('$.city')")],
            OrderBy =
            [
                "COUNT()".Desc(),
                "payload.JsonGetText('$.city')".Asc()
            ]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Texts("city"))
            .IsEquivalentTo(["Moscow", "Paris"], CollectionOrdering.Matching);
        await Assert.That(rows.Ints("count"))
            .IsEquivalentTo([2, 1], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query применяет date выражения в WHERE и ORDER BY")]
    public async Task Date_expression_works_in_where_and_order_by()
    {
        // Arrange
        var source = InlineQueryArrange.Source(
            [
                new InlineField("id", DataType.Integer),
                new InlineField("created_at", DataType.Text)
            ],
            [
                ["1", "'2026-01-01'"],
                ["2", "'2026-01-03'"],
                ["3", "'2025-12-31'"],
                ["4", "'bad'"]
            ]);
        var query = new Query.Models.Query
        {
            Source = source,
            Select =
            [
                "id".As("id"),
                "created_at.Date('yyyy-MM-dd').Text('yyyy-MM-dd')".As("date_text")
            ],
            Where = Expr("created_at.Date('yyyy-MM-dd') >= Date('2026-01-01', 'yyyy-MM-dd')"),
            OrderBy = ["created_at.Date('yyyy-MM-dd')".Desc()]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Ints("id"))
            .IsEquivalentTo([2, 1], CollectionOrdering.Matching);
        await Assert.That(rows.Texts("date_text"))
            .IsEquivalentTo(["2026-01-03", "2026-01-01"], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query поддерживает alias полей источника со спецсимволами через экранированные шаблоны")]
    public async Task Escaped_source_field_aliases_work()
    {
        // Arrange
        var source = InlineQueryArrange.Source(
            [
                new InlineField("field with space", DataType.Text),
                new InlineField("where", DataType.Integer),
                new InlineField("back`tick", DataType.Text)
            ],
            [
                ["'Mike'", "2", "'A'"],
                ["'Ann'", "1", "'B'"]
            ]);
        var query = new Query.Models.Query
        {
            Source = source,
            Select =
            [
                "[field with space]".As("name"),
                "[where] + 1".As("rank"),
                "[back`tick]".As("mark")
            ],
            OrderBy = ["[where]".Asc()]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Texts("name"))
            .IsEquivalentTo(["Ann", "Mike"], CollectionOrdering.Matching);
        await Assert.That(rows.Ints("rank"))
            .IsEquivalentTo([2, 3], CollectionOrdering.Matching);
        await Assert.That(rows.Texts("mark"))
            .IsEquivalentTo(["B", "A"], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query field alias может совпадать с именем функции")]
    public async Task Field_alias_can_shadow_function_name()
    {
        // Arrange
        var source = InlineQueryArrange.Source(
            [
                new InlineField("Upper", DataType.Text),
                new InlineField("COUNT", DataType.Integer),
                new InlineField("Date", DataType.Text)
            ],
            [
                ["'mike'", "1", "'2026-01-01'"]
            ]);
        var query = new Query.Models.Query
        {
            Source = source,
            Select =
            [
                "Upper".As("upper_field"),
                "Upper(Upper)".As("upper_function"),
                "COUNT".As("count_field"),
                "Date.Date('yyyy-MM-dd').Text('yyyy')".As("year")
            ]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows[0].Text("upper_field")).IsEqualTo("mike");
        await Assert.That(rows[0].Text("upper_function")).IsEqualTo("MIKE");
        await Assert.That(rows[0].Int("count_field")).IsEqualTo(1);
        await Assert.That(rows[0].Text("year")).IsEqualTo("2026");
    }

    [Test]
    [DisplayName("Query учитывает регистр alias полей источника")]
    public async Task Source_field_aliases_are_case_sensitive()
    {
        // Arrange
        var source = InlineQueryArrange.Source(
            [
                new InlineField("city", DataType.Text),
                new InlineField("City", DataType.Text)
            ],
            [
                ["'lower'", "'upper'"]
            ]);
        var query = new Query.Models.Query
        {
            Source = source,
            Select =
            [
                "city".As("city"),
                "City".As("City")
            ]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows[0].Text("city")).IsEqualTo("lower");
        await Assert.That(rows[0].Text("City")).IsEqualTo("upper");
    }

    [Test]
    [DisplayName("Query выходные поля получают типы и nullable из resolved выражений")]
    public async Task Output_fields_have_resolved_types()
    {
        // Arrange
        var source = InlineQueryArrange.Source(
            [
                new InlineField("id", DataType.Integer),
                new InlineField("score", DataType.Number, CanBeNull: true),
                new InlineField("flag", DataType.Boolean, CanBeNull: true),
                new InlineField("text", DataType.Text, CanBeNull: true)
            ],
            [
                ["1", "10.0", "true", "'2026-01-01'"]
            ]);
        var query = new Query.Models.Query
        {
            Source = source,
            Select =
            [
                "id + 1".As("next_id"),
                "score + 1".As("next_score"),
                "flag".As("flag"),
                "text.Date('yyyy-MM-dd')".As("date")
            ]
        };

        // Act
        var resolved = new QueryResolver().Resolve(query, ClickHouseFunctions.CreateResolver()).Value!;

        // Assert
        await AssertOutputFieldAsync(resolved.OutputFields[0], "next_id", DataType.Integer, canBeNull: false);
        await AssertOutputFieldAsync(resolved.OutputFields[1], "next_score", DataType.Number, canBeNull: true);
        await AssertOutputFieldAsync(resolved.OutputFields[2], "flag", DataType.Boolean, canBeNull: true);
        await AssertOutputFieldAsync(resolved.OutputFields[3], "date", DataType.DateTime, canBeNull: true);
    }

    private static QuerySource NullableBoolSource()
    {
        return InlineQueryArrange.Source(
            [
                new InlineField("id", DataType.Integer),
                new InlineField("flag", DataType.Boolean, CanBeNull: true)
            ],
            [
                ["1", "true"],
                ["2", "false"],
                ["3", "NULL"]
            ]);
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

    private static QuerySource NullableNumbersSource()
    {
        return InlineQueryArrange.Source(
            [
                new InlineField("id", DataType.Integer),
                new InlineField("score", DataType.Number, CanBeNull: true)
            ],
            [
                ["1", "10.0"],
                ["2", "NULL"],
                ["3", "7.5"],
                ["4", "NULL"]
            ]);
    }

    private static QuerySource CityAmountsSource()
    {
        return InlineQueryArrange.Source(
            [
                new InlineField("city", DataType.Text),
                new InlineField("amount", DataType.Number)
            ],
            [
                ["'Moscow'", "10.0"],
                ["'Paris'", "20.0"]
            ]);
    }

    private static Expr Expr(string expression)
    {
        return Loader.Lang.Expressions.Expr.Parse(expression).Value;
    }

    private static async Task AssertNumberAsync(object? actual, double expected)
    {
        await Assert.That(Convert.ToDouble(actual, CultureInfo.InvariantCulture))
            .IsEqualTo(expected)
            .Within(0.000001);
    }

    private static async Task AssertOutputFieldAsync(
        Field field,
        string alias,
        DataType dataType,
        bool canBeNull)
    {
        await Assert.That(field.Alias).IsEqualTo(alias);
        await Assert.That(field.Type.DataType).IsEqualTo(dataType);
        await Assert.That(field.Type.CanBeNull).IsEqualTo(canBeNull);
    }
}

internal static class QueryEdgeCaseExecutionTestExtensions
{
    public static object?[] Values(this IEnumerable<IReadOnlyDictionary<string, object?>> rows, string name)
    {
        return rows.Select(row => row[name]).ToArray();
    }
}

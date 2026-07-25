using System.Globalization;
using Loader.Lang.Expressions;
using Loader.Query.Models;
using Loader.Query.Tests.Infrastructure;

namespace Loader.Query.Tests.Functions.Aggregation;

public sealed class ClickHouseAggregationExecutionTests : ClickHouseExpressionTestBase
{
    public ClickHouseAggregationExecutionTests(ClickHouseTestDatabase database)
        : base(database)
    {
    }

    [Test]
    [DisplayName("SUM(int): суммирует integer значения")]
    public async Task Sum_integer()
    {
        int?[] values = [1, 2, 3, 4, 5];
        var inline = CreateSingleColumnInline(DataType.Integer, ToExpressions(values));
        var query = CreateSingleColumnQuery(inline, "SUM(x)");

        var result = await GetScalarAsync(query);

        await AssertNumberAsync(result, 15);
    }

    [Test]
    [DisplayName("SUM(int): игнорирует NULL")]
    public async Task Sum_integer_with_nulls()
    {
        int?[] values = [1, null, 3, null, 5];
        var inline = CreateSingleColumnInline(DataType.Integer, ToExpressions(values));
        var query = CreateSingleColumnQuery(inline, "SUM(x)");

        var result = await GetScalarAsync(query);

        await AssertNumberAsync(result, 9);
    }

    [Test]
    [DisplayName("SUM(int): пустой набор возвращает 0")]
    public async Task Sum_integer_empty()
    {
        int?[] values = [1];
        var inline = CreateSingleColumnInline(DataType.Integer, ToExpressions(values));
        var query = CreateSingleColumnQuery(inline, "SUM(x)", where: "false");

        var result = await GetScalarAsync(query);

        await AssertNumberAsync(result, 0);
    }

    [Test]
    [DisplayName("SUM(num): суммирует number значения")]
    public async Task Sum_number()
    {
        double?[] values = [1.5, 2.5, 3.5];
        var inline = CreateSingleColumnInline(DataType.Number, ToExpressions(values));
        var query = CreateSingleColumnQuery(inline, "SUM(x)");

        var result = await GetScalarAsync(query);

        await AssertNumberAsync(result, 7.5);
    }

    [Test]
    [DisplayName("AVG(int): считает среднее integer значений")]
    public async Task Avg_integer()
    {
        int?[] values = [1, 2, 3, 4, 5];
        var inline = CreateSingleColumnInline(DataType.Integer, ToExpressions(values));
        var query = CreateSingleColumnQuery(inline, "AVG(x)");

        var result = await GetScalarAsync(query);

        await AssertNumberAsync(result, 3.0);
    }

    [Test]
    [DisplayName("AVG(int): пустой набор возвращает NULL")]
    public async Task Avg_integer_empty()
    {
        int?[] values = [1, 2, 3];
        var inline = CreateSingleColumnInline(DataType.Integer, ToExpressions(values));
        var query = CreateSingleColumnQuery(inline, "AVG(x)", where: "false");

        var result = await GetScalarAsync(query);

        await Assert.That(result).IsNull();
    }

    [Test]
    [DisplayName("MIN/MAX: работают для integer")]
    public async Task Min_max_integer()
    {
        int?[] values = [5, null, 3, null, 8];
        var inline = CreateSingleColumnInline(DataType.Integer, ToExpressions(values));
        var minQuery = CreateSingleColumnQuery(inline, "MIN(x)");
        var maxQuery = CreateSingleColumnQuery(inline, "MAX(x)");

        var min = await GetScalarAsync(minQuery);
        var max = await GetScalarAsync(maxQuery);

        await AssertNumberAsync(min, 3);
        await AssertNumberAsync(max, 8);
    }

    [Test]
    [DisplayName("MIN/MAX: работают для text")]
    public async Task Min_max_text()
    {
        string?[] values = ["banana", null, "apple", "cherry"];
        var inline = CreateSingleColumnInline(DataType.Text, ToExpressions(values));
        var minQuery = CreateSingleColumnQuery(inline, "MIN(x)");
        var maxQuery = CreateSingleColumnQuery(inline, "MAX(x)");

        var min = await GetScalarAsync(minQuery);
        var max = await GetScalarAsync(maxQuery);

        await Assert.That(min).IsEqualTo("apple");
        await Assert.That(max).IsEqualTo("cherry");
    }

    [Test]
    [DisplayName("COUNT(): считает все строки")]
    public async Task Count_all_rows()
    {
        int?[] values = [1, null, 3, null, 5];
        var inline = CreateSingleColumnInline(DataType.Integer, ToExpressions(values));
        var query = CreateSingleColumnQuery(inline, "COUNT()");

        var result = await GetScalarAsync(query);

        await AssertNumberAsync(result, 5);
    }

    [Test]
    [DisplayName("COUNT(field): считает только non-null значения")]
    public async Task Count_column()
    {
        int?[] values = [1, null, 3, null, 5];
        var inline = CreateSingleColumnInline(DataType.Integer, ToExpressions(values));
        var query = CreateSingleColumnQuery(inline, "COUNT(x)");

        var result = await GetScalarAsync(query);

        await AssertNumberAsync(result, 3);
    }

    [Test]
    [DisplayName("COUNT_DISTINCT: считает уникальные non-null значения")]
    public async Task Count_distinct_integer()
    {
        int?[] values = [1, null, 2, null, 1];
        var inline = CreateSingleColumnInline(DataType.Integer, ToExpressions(values));
        var query = CreateSingleColumnQuery(inline, "COUNT_DISTINCT(x)");

        var result = await GetScalarAsync(query);

        await AssertNumberAsync(result, 2);
    }

    [Test]
    [DisplayName("ONLY: возвращает единственное уникальное non-null значение")]
    public async Task Only_single_value_with_nulls()
    {
        int?[] values = [5, null, null];
        var inline = CreateSingleColumnInline(DataType.Integer, ToExpressions(values));
        var query = CreateSingleColumnQuery(inline, "ONLY(x)");

        var result = await GetScalarAsync(query);

        await AssertNumberAsync(result, 5);
    }

    [Test]
    [DisplayName("ONLY: несколько значений возвращают NULL")]
    public async Task Only_multiple_values()
    {
        int?[] values = [5, 6];
        var inline = CreateSingleColumnInline(DataType.Integer, ToExpressions(values));
        var query = CreateSingleColumnQuery(inline, "ONLY(x)");

        var result = await GetScalarAsync(query);

        await Assert.That(result).IsNull();
    }

    [Test]
    [DisplayName("CONCAT(value): склеивает non-null text")]
    public async Task Concat_basic()
    {
        string?[] values = ["a", null, "c"];
        var inline = CreateSingleColumnInline(DataType.Text, ToExpressions(values));
        var query = CreateSingleColumnQuery(inline, "CONCAT(x)");

        var result = await GetScalarAsync(query);

        await Assert.That(result).IsEqualTo("ac");
    }

    [Test]
    [DisplayName("CONCAT(value, delimiter): склеивает non-null text с разделителем")]
    public async Task Concat_with_delimiter()
    {
        string?[] values = ["a", null, "c"];
        var inline = CreateSingleColumnInline(DataType.Text, ToExpressions(values));
        var query = CreateSingleColumnQuery(inline, "CONCAT(x, '|')");

        var result = await GetScalarAsync(query);

        await Assert.That(result).IsEqualTo("a|c");
    }

    [Test]
    [DisplayName("MODE(text): возвращает моду текстового поля")]
    public async Task Mode_text()
    {
        string?[] values = ["a", "b", "a", null];
        var inline = CreateSingleColumnInline(DataType.Text, ToExpressions(values));
        var query = CreateSingleColumnQuery(inline, "MODE(x)");

        var result = await GetScalarAsync(query);

        await Assert.That(result).IsEqualTo("a");
    }

    [Test]
    [DisplayName("MEDIAN(num): возвращает медиану")]
    public async Task Median_number()
    {
        double?[] values = [1, 2, 3, 4];
        var inline = CreateSingleColumnInline(DataType.Number, ToExpressions(values));
        var query = CreateSingleColumnQuery(inline, "MEDIAN(x)");

        var result = await GetScalarAsync(query);

        await AssertNumberAsync(result, 2.5);
    }

    private static QuerySource CreateSingleColumnInline(DataType dataType, IReadOnlyList<string> values)
    {
        return InlineQueryArrange.SingleColumnSource("x", dataType, values);
    }

    private static Query.Models.Query CreateSingleColumnQuery(
        QuerySource source,
        string expression,
        string? where = null)
    {
        return new Query.Models.Query
        {
            Source = source,
            Select =
            [
                new SelectItem
                {
                    Alias = "test",
                    Expression = Expr.Parse(expression).Value
                }
            ],
            Where = where is null ? null : Expr.Parse(where).Value
        };
    }

    private static string[] ToExpressions(int?[] values)
    {
        return values.Select(static value => value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "null").ToArray();
    }

    private static string[] ToExpressions(double?[] values)
    {
        return values.Select(static value => value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "null").ToArray();
    }

    private static string[] ToExpressions(string?[] values)
    {
        return values.Select(static value => value is null ? "null" : $"'{value}'").ToArray();
    }

    private static async Task AssertNumberAsync(object? actual, double expected)
    {
        await Assert.That(Convert.ToDouble(actual, CultureInfo.InvariantCulture))
            .IsEqualTo(expected)
            .Within(0.000001);
    }
}

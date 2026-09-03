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
    [DisplayName("STDDEV(int): считает population standard deviation")]
    public async Task Stddev_integer()
    {
        int?[] values = [2, 4, 4, 4, 5, 5, 7, 9];
        var inline = CreateSingleColumnInline(DataType.Integer, ToExpressions(values));
        var query = CreateSingleColumnQuery(inline, "STDDEV(x)");

        var result = await GetScalarAsync(query);

        await AssertNumberAsync(result, 2);
    }

    [Test]
    [DisplayName("STDDEV(num): игнорирует NULL")]
    public async Task Stddev_number_with_nulls()
    {
        double?[] values = [2, null, 4, 4, 4, 5, 5, 7, 9, null];
        var inline = CreateSingleColumnInline(DataType.Number, ToExpressions(values));
        var query = CreateSingleColumnQuery(inline, "STDDEV(x)");

        var result = await GetScalarAsync(query);

        await AssertNumberAsync(result, 2);
    }

    [Test]
    [DisplayName("STDDEV(Num(text)): работает с Decimal результатом Num")]
    public async Task Stddev_number_from_num_text_decimal()
    {
        string?[] values = ["2", null, "4", "4", "4", "5", "5", "7", "9"];
        var inline = CreateSingleColumnInline(DataType.Text, ToExpressions(values));
        var query = CreateSingleColumnQuery(inline, "STDDEV(Num(x))");

        var result = await GetScalarAsync(query);

        await AssertNumberAsync(result, 2);
    }

    [Test]
    [DisplayName("STDDEV(num): пустой набор возвращает NULL")]
    public async Task Stddev_empty_returns_null()
    {
        double?[] values = [1, 2, 3];
        var inline = CreateSingleColumnInline(DataType.Number, ToExpressions(values));
        var query = CreateSingleColumnQuery(inline, "STDDEV(x)", where: "false");

        var result = await GetScalarAsync(query);

        await Assert.That(result).IsNull();
    }

    [Test]
    [DisplayName("CORREL(num, num): считает корреляцию Пирсона")]
    public async Task Correl_number_number()
    {
        var inline = InlineQueryArrange.Source(
            [
                new InlineField("x", DataType.Number),
                new InlineField("y", DataType.Number)
            ],
            [
                ["1.0", "2.0"],
                ["2.0", "4.0"],
                ["3.0", "6.0"],
                ["4.0", "8.0"]
            ]);
        var query = CreateSingleColumnQuery(inline, "CORREL(x, y)");

        var result = await GetScalarAsync(query);

        await AssertNumberAsync(result, 1);
    }

    [Test]
    [DisplayName("CORREL(int, num): игнорирует строки с NULL")]
    public async Task Correl_integer_number_with_nulls()
    {
        var inline = InlineQueryArrange.Source(
            [
                new InlineField("x", DataType.Integer),
                new InlineField("y", DataType.Number)
            ],
            [
                ["1", "2.0"],
                ["2", "NULL"],
                ["3", "6.0"],
                ["NULL", "8.0"],
                ["4", "8.0"]
            ]);
        var query = CreateSingleColumnQuery(inline, "CORREL(x, y)");

        var result = await GetScalarAsync(query);

        await AssertNumberAsync(result, 1);
    }

    [Test]
    [DisplayName("CORREL(Num(text), Num(text)): работает с Decimal результатом Num")]
    public async Task Correl_number_from_num_text_decimal()
    {
        var inline = InlineQueryArrange.Source(
            [
                new InlineField("x", DataType.Text),
                new InlineField("y", DataType.Text)
            ],
            [
                ["'1.25'", "'2.50'"],
                ["'2.50'", "'5.00'"],
                ["'3.75'", "'7.50'"],
                ["'5.00'", "'10.00'"]
            ]);
        var query = CreateSingleColumnQuery(inline, "CORREL(Num(x), Num(y))");

        var result = await GetScalarAsync(query);

        await AssertNumberAsync(result, 1);
    }

    [Test]
    [DisplayName("CORREL(num, num): пустой набор возвращает NULL")]
    public async Task Correl_empty_returns_null()
    {
        var inline = InlineQueryArrange.Source(
            [
                new InlineField("x", DataType.Number),
                new InlineField("y", DataType.Number)
            ],
            [
                ["1.0", "2.0"],
                ["2.0", "4.0"]
            ]);
        var query = CreateSingleColumnQuery(inline, "CORREL(x, y)", where: "false");

        var result = await GetScalarAsync(query);

        await Assert.That(result).IsNull();
    }

    [Test]
    [DisplayName("AVG(date): считает среднюю дату с точностью до секунды")]
    public async Task Avg_datetime()
    {
        var inline = CreateSingleColumnInline(
            DataType.DateTime,
            [
                "toDateTime('2023-01-01 00:00:00')",
                "NULL",
                "toDateTime('2023-01-03 00:00:00')"
            ]);
        var query = CreateSingleColumnQuery(inline, "AVG(x)");

        var result = await GetScalarAsync(query);

        await AssertDateTimeAsync(result, "2023-01-02 00:00:00");
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
    [DisplayName("MIN/MAX: работают для bool")]
    public async Task Min_max_boolean()
    {
        bool?[] values = [true, null, false, true];
        var inline = CreateSingleColumnInline(DataType.Boolean, ToExpressions(values));
        var minQuery = CreateSingleColumnQuery(inline, "MIN(x)");
        var maxQuery = CreateSingleColumnQuery(inline, "MAX(x)");

        var min = await GetScalarAsync(minQuery);
        var max = await GetScalarAsync(maxQuery);

        await Assert.That((bool)min!).IsFalse();
        await Assert.That((bool)max!).IsTrue();
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
    [DisplayName("COUNT(поле): работает для bool")]
    public async Task Count_boolean_column()
    {
        bool?[] values = [true, null, false, null, true];
        var inline = CreateSingleColumnInline(DataType.Boolean, ToExpressions(values));
        var query = CreateSingleColumnQuery(inline, "COUNT(x)");

        var result = await GetScalarAsync(query);

        await AssertNumberAsync(result, 3);
    }

    [Test]
    [DisplayName("COUNT_IF(bool): считает только true значения")]
    public async Task Count_if_boolean_condition()
    {
        bool?[] values = [true, null, false, true, false];
        var inline = CreateSingleColumnInline(DataType.Boolean, ToExpressions(values));
        var query = CreateSingleColumnQuery(inline, "COUNT_IF(x)");

        var result = await GetScalarAsync(query);

        await AssertNumberAsync(result, 2);
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
    [DisplayName("COUNT_DISTINCT: работает для text")]
    public async Task Count_distinct_text()
    {
        string?[] values = ["a", null, "b", "a"];
        var inline = CreateSingleColumnInline(DataType.Text, ToExpressions(values));
        var query = CreateSingleColumnQuery(inline, "COUNT_DISTINCT(x)");

        var result = await GetScalarAsync(query);

        await AssertNumberAsync(result, 2);
    }

    [Test]
    [DisplayName("COUNT_DISTINCT: работает для bool")]
    public async Task Count_distinct_boolean()
    {
        bool?[] values = [true, null, false, true];
        var inline = CreateSingleColumnInline(DataType.Boolean, ToExpressions(values));
        var query = CreateSingleColumnQuery(inline, "COUNT_DISTINCT(x)");

        var result = await GetScalarAsync(query);

        await AssertNumberAsync(result, 2);
    }

    [Test]
    [DisplayName("COUNT_DISTINCT: работает для time")]
    public async Task Count_distinct_time()
    {
        var inline = CreateSingleColumnInline(
            DataType.Time,
            [
                "toDateTime('1970-01-01 03:04:05')",
                "NULL",
                "toDateTime('1970-01-01 03:04:05')",
                "toDateTime('1970-01-01 05:06:07')"
            ]);
        var query = CreateSingleColumnQuery(inline, "COUNT_DISTINCT(x)");

        var result = await GetScalarAsync(query);

        await AssertNumberAsync(result, 2);
    }

    [Test]
    [DisplayName("ONLY: возвращает единственное повторяющееся значение")]
    public async Task Only_single_repeated_value()
    {
        int?[] values = [5, 5, 5];
        var inline = CreateSingleColumnInline(DataType.Integer, ToExpressions(values));
        var query = CreateSingleColumnQuery(inline, "ONLY(x)");

        var result = await GetScalarAsync(query);

        await AssertNumberAsync(result, 5);
    }

    [Test]
    [DisplayName("ONLY: NULL считается отдельным значением")]
    public async Task Only_null_makes_value_not_unique()
    {
        int?[] values = [5, 5, null];
        var inline = CreateSingleColumnInline(DataType.Integer, ToExpressions(values));
        var query = CreateSingleColumnQuery(inline, "ONLY(x)");

        var result = await GetScalarAsync(query);

        await Assert.That(result).IsNull();
    }

    [Test]
    [DisplayName("ONLY: все NULL возвращают NULL")]
    public async Task Only_all_nulls()
    {
        int?[] values = [null, null];
        var inline = CreateSingleColumnInline(DataType.Integer, ToExpressions(values));
        var query = CreateSingleColumnQuery(inline, "ONLY(x)");

        var result = await GetScalarAsync(query);

        await Assert.That(result).IsNull();
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
    [DisplayName("ONLY: работает для text")]
    public async Task Only_text()
    {
        string?[] values = ["a", "a"];
        var inline = CreateSingleColumnInline(DataType.Text, ToExpressions(values));
        var query = CreateSingleColumnQuery(inline, "ONLY(x)");

        var result = await GetScalarAsync(query);

        await Assert.That(result).IsEqualTo("a");
    }

    [Test]
    [DisplayName("ONLY: работает для time")]
    public async Task Only_time()
    {
        var inline = CreateSingleColumnInline(
            DataType.Time,
            [
                "toDateTime('1970-01-01 03:04:05')",
                "toDateTime('1970-01-01 03:04:05')"
            ]);
        var query = CreateSingleColumnQuery(inline, "ONLY(x)");

        var result = await GetScalarAsync(query);

        await AssertDateTimeAsync(result, "1970-01-01 03:04:05");
    }

    [Test]
    [DisplayName("ONLY: работает для bool")]
    public async Task Only_boolean()
    {
        bool?[] values = [true, true];
        var inline = CreateSingleColumnInline(DataType.Boolean, ToExpressions(values));
        var query = CreateSingleColumnQuery(inline, "ONLY(x)");

        var result = await GetScalarAsync(query);

        await Assert.That((bool)result!).IsTrue();
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
    [DisplayName("CONCAT(value, delimiter, sort): склеивает text после сортировки")]
    public async Task Concat_with_delimiter_and_sort()
    {
        var inline = InlineQueryArrange.Source(
            [
                new InlineField("x", DataType.Text),
                new InlineField("sort", DataType.Integer)
            ],
            [
                ["'a'", "2"],
                ["NULL", "3"],
                ["'b'", "1"],
                ["'c'", "4"]
            ]);
        var query = CreateSingleColumnQuery(inline, "CONCAT(x, '|', sort)");

        var result = await GetScalarAsync(query);

        await Assert.That(result).IsEqualTo("b|a|c");
    }

    [Test]
    [DisplayName("CONCAT(value): all-null набор возвращает NULL")]
    public async Task Concat_all_null()
    {
        string?[] values = [null, null];
        var inline = CreateSingleColumnInline(DataType.Text, ToExpressions(values));
        var query = CreateSingleColumnQuery(inline, "CONCAT(x)");

        var result = await GetScalarAsync(query);

        await Assert.That(result).IsNull();
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
    [DisplayName("MODE(text): all-null набор возвращает NULL")]
    public async Task Mode_all_null()
    {
        string?[] values = [null, null];
        var inline = CreateSingleColumnInline(DataType.Text, ToExpressions(values));
        var query = CreateSingleColumnQuery(inline, "MODE(x)");

        var result = await GetScalarAsync(query);

        await Assert.That(result).IsNull();
    }

    [Test]
    [DisplayName("MODE(bool): возвращает самое частое не NULL значение")]
    public async Task Mode_boolean()
    {
        bool?[] values = [true, false, true, null];
        var inline = CreateSingleColumnInline(DataType.Boolean, ToExpressions(values));
        var query = CreateSingleColumnQuery(inline, "MODE(x)");

        var result = await GetScalarAsync(query);

        await Assert.That((bool)result!).IsTrue();
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

    [Test]
    [DisplayName("FRACTILE(num, p): возвращает непрерывную квантиль по константному p")]
    public async Task Fractile_number()
    {
        double?[] values = [1, 2, 3, 4];
        var inline = CreateSingleColumnInline(DataType.Number, ToExpressions(values));
        var query = CreateSingleColumnQuery(inline, "FRACTILE(x, 0.25)");

        var result = await GetScalarAsync(query);

        await AssertNumberAsync(result, 1.75);
    }

    [Test]
    [DisplayName("FRACTILE(int, p): работает для integer значений")]
    public async Task Fractile_integer()
    {
        int?[] values = [1, 2, 3, 4];
        var inline = CreateSingleColumnInline(DataType.Integer, ToExpressions(values));
        var query = CreateSingleColumnQuery(inline, "FRACTILE(x, 0.75)");

        var result = await GetScalarAsync(query);

        await AssertNumberAsync(result, 3.25);
    }

    [Test]
    [DisplayName("FRACTILE(num, p): игнорирует NULL")]
    public async Task Fractile_number_with_nulls()
    {
        double?[] values = [1, null, 2, 3, 4, null];
        var inline = CreateSingleColumnInline(DataType.Number, ToExpressions(values));
        var query = CreateSingleColumnQuery(inline, "FRACTILE(x, 0.5)");

        var result = await GetScalarAsync(query);

        await AssertNumberAsync(result, 2.5);
    }

    [Test]
    [DisplayName("FRACTILE(num, p): требует константный p")]
    public async Task Fractile_requires_constant_p()
    {
        var source = InlineQueryArrange.Source(
            [
                new InlineField("x", DataType.Number),
                new InlineField("p", DataType.Number)
            ],
            [
                ["1.0", "0.5"],
                ["2.0", "0.5"]
            ]);
        var query = new Query.Models.Query
        {
            Source = source,
            Select =
            [
                new SelectItem
                {
                    Alias = "test",
                    Expression = Expr.Parse("FRACTILE(x, p)").Value
                }
            ]
        };

        await Assert.That(async () => await GetScalarAsync(query))
            .ThrowsExactly<InvalidOperationException>()
            .WithMessage("Функция 'FRACTILE' требует, чтобы аргумент 2 был константой");
    }

    [Test]
    [Arguments("FRACTILE(x, 1.1)")]
    [Arguments("FRACTILE(x, 2)")]
    [DisplayName("FRACTILE отклоняет p вне диапазона от 0 до 1 на resolve")]
    public async Task Fractile_rejects_p_outside_supported_range(string expression)
    {
        var inline = CreateSingleColumnInline(DataType.Number, ["1.0", "2.0", "3.0"]);
        var query = CreateSingleColumnQuery(inline, expression);

        await Assert.That(async () => await GetScalarAsync(query))
            .ThrowsExactly<InvalidOperationException>()
            .WithMessage("Функция 'FRACTILE' требует, чтобы аргумент 2 был в диапазоне 0..1");
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

    private static string[] ToExpressions(bool?[] values)
    {
        return values.Select(static value => value.HasValue ? value.Value.ToString().ToLowerInvariant() : "null").ToArray();
    }

    private static async Task AssertNumberAsync(object? actual, double expected)
    {
        await Assert.That(Convert.ToDouble(actual, CultureInfo.InvariantCulture))
            .IsEqualTo(expected)
            .Within(0.000001);
    }

    private static async Task AssertDateTimeAsync(object? actual, string expected)
    {
        await Assert.That(Convert.ToDateTime(actual, CultureInfo.InvariantCulture))
            .IsEqualTo(DateTime.Parse(expected, CultureInfo.InvariantCulture));
    }
}

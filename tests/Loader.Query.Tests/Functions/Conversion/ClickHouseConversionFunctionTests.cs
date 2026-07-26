using Loader.Query.Tests.Infrastructure;
using Loader.Lang.Expressions;
using Loader.Query.Functions;
using Loader.Query.Models;
using Loader.Query.Resolve;

namespace Loader.Query.Tests.Functions.Conversion;

public sealed class ClickHouseConversionFunctionTests : ClickHouseExpressionTestBase
{
    public ClickHouseConversionFunctionTests(ClickHouseTestDatabase database)
        : base(database)
    {
    }

    [Test]
    [Arguments("Int(25)", 25)]
    [Arguments("Int('25')", 25)]
    [Arguments("Int(2.6)", 2)]
    [Arguments("If(Int(3.9) > 3.4, 1, 0)", 0)]
    [Arguments("Int(false)", 0)]
    [Arguments("Int(true)", 1)]
    [Arguments("Int(null)", null)]
    public Task Int(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("Num(25)", 25.0)]
    [Arguments("Num('25')", 25.0)]
    [Arguments("Num(2.5)", 2.5)]
    [Arguments("Num(false)", 0.0)]
    [Arguments("Num(true)", 1.0)]
    [Arguments("Num(null)", null)]
    public Task Num(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("Text(25)", "25")]
    [Arguments("Text(25000000)", "25000000")]
    [Arguments("Text('25')", "25")]
    [Arguments("Text(2.5)", "2.5")]
    [Arguments("Text(1000000.123)", "1000000.123")]
    [Arguments("Text(false)", "false")]
    [Arguments("Text(true)", "true")]
    [Arguments("Text(null)", null)]
    [Arguments("Date('2025-03-27 21:19').Text()", "2025-03-27 21:19:00")]
    [Arguments("Date('2025-03-27').Text()", "2025-03-27 00:00:00")]
    [Arguments("Date('2026-01-02 15:04:05').Text('yyyy-MM-dd')", "2026-01-02")]
    [Arguments("Date('2026-01-02 15:04:05').Text('dd.MM.yyyy hh:mm:ss a')", "02.01.2026 03:04:05 PM")]
    [Arguments("Date('2026-01-02 15:04:05').Text('dd MMMM yyyy')", "02 January 2026")]
    [Arguments("Date('2026-01-02', 'yyyy-MM-dd').DateOnly().Text('dd MMM yyyy')", "02 Jan 2026")]
    [Arguments("Date('2026-01-02', 'yyyy-MM-dd').DateOnly().Text('yyyy-MM-dd')", "2026-01-02")]
    [Arguments("Date(null, 'yyyy-MM-dd').DateOnly().Text('yyyy-MM-dd')", null)]
    public Task Text(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("Bool(25)", true)]
    [Arguments("Bool(0)", false)]
    [Arguments("Bool(-5)", false)]
    [Arguments("Bool(23)", true)]
    [Arguments("Bool(0.0)", false)]
    [Arguments("Bool(-5.0)", false)]
    [Arguments("Bool(23.0)", true)]
    [Arguments("Bool('25')", true)]
    [Arguments("Bool('')", false)]
    [Arguments("Bool(false)", false)]
    [Arguments("Bool(true)", true)]
    [Arguments("If(Bool(null), 'then', 'else')", "else")]
    public Task Bool(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("Date('2025-03-27 21:19')", "@2025-03-27 21:19")]
    [Arguments("'2025-03-27 21:40'.Date()", "@2025-03-27 21:40")]
    [Arguments("Date('2025-03-27')", "@2025-03-27 00:00")]
    [Arguments("''.EmptyIsNull().Date()", null)]
    [Arguments("Date('2025-03-27').Date().Date()", "@2025-03-27 00:00")]
    public Task Date(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [Arguments("RawType(Int('1'))", "Int64")]
    [Arguments("RawType(Num('1'))", "Decimal(18, 10)")]
    [Arguments("RawType(Bool('a'))", "Bool")]
    [DisplayName("ClickHouse conversion casts выбирают non-nullable тип для non-nullable выражений")]
    public Task Conversion_casts_use_expected_clickhouse_type_for_required_expression(string expression, object? expected)
    {
        return AssertExpressionAsync(expression, expected);
    }

    [Test]
    [MethodDataSource(nameof(NullableConversionTypeCases))]
    [DisplayName("ClickHouse conversion casts выбирают Nullable тип для nullable выражений")]
    public async Task Conversion_casts_use_expected_clickhouse_type_for_nullable_expression(
        DataType sourceType,
        string sourceValue,
        string expression,
        string expectedType)
    {
        // Arrange
        var source = InlineQueryArrange.SingleColumnSource(
            "x",
            sourceType,
            [sourceValue],
            canBeNull: true);
        var query = new Query.Models.Query
        {
            Source = source,
            Select = [Select("type", $"RawType({expression})")]
        };

        // Act
        var type = await GetScalarAsync(query);

        // Assert
        await Assert.That(type).IsEqualTo(expectedType);
    }

    [Test]
    [MethodDataSource(nameof(NullableConversionCases))]
    [DisplayName("ClickHouse conversion function сохраняет NULL из nullable source field")]
    public async Task Nullable_field_conversion_preserves_null(
        DataType sourceType,
        string sourceValue,
        string expression)
    {
        // Arrange
        var source = InlineQueryArrange.SingleColumnSource(
            "x",
            sourceType,
            [sourceValue, "NULL"],
            canBeNull: true);
        var query = new Query.Models.Query
        {
            Source = source,
            Select = [Select("value", expression)]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows).Count().IsEqualTo(2);
        await Assert.That(rows.Select(static row => row["value"]).Any(static value => value is null)).IsTrue();
    }

    [Test]
    [DisplayName("Text date format должен быть константой")]
    public async Task Text_date_format_must_be_constant()
    {
        var source = InlineQueryArrange.SingleColumnSource(
            "Format",
            DataType.Text,
            ["'yyyy-MM-dd'"],
            canBeNull: false);
        var query = new Query.Models.Query
        {
            Source = source,
            Select = [Select("value", "Date('2026-01-02').Text(Format)")]
        };

        var result = new QueryResolver().Resolve(query, ClickHouseFunctions.CreateResolver());

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Errors.Select(static error => error.Message).ToArray())
            .IsEquivalentTo(["Функция 'Text' требует, чтобы аргумент 2 был константой"]);
    }

    public static IEnumerable<(DataType SourceType, string SourceValue, string Expression)> NullableConversionCases()
    {
        yield return (DataType.Text, "'25'", "Int(x)");
        yield return (DataType.Text, "'25'", "Num(x)");
        yield return (DataType.Text, "'abc'", "Bool(x)");
        yield return (DataType.Text, "'2026-01-02'", "Date(x)");
        yield return (DataType.Integer, "25", "Text(x)");
        yield return (DataType.Number, "25.5", "Text(x)");
        yield return (DataType.Boolean, "true", "Text(x)");
        yield return (DataType.Number, "25.5", "Int(x)");
        yield return (DataType.Integer, "25", "Num(x)");
        yield return (DataType.Boolean, "true", "Int(x)");
        yield return (DataType.Boolean, "true", "Num(x)");
    }

    public static IEnumerable<(DataType SourceType, string SourceValue, string Expression, string ExpectedType)> NullableConversionTypeCases()
    {
        yield return (DataType.Text, "'25'", "Int(x)", "Nullable(Int64)");
        yield return (DataType.Text, "'25'", "Num(x)", "Nullable(Decimal(18, 10))");
        yield return (DataType.Text, "'abc'", "Bool(x)", "Nullable(Bool)");
        yield return (DataType.Number, "25.5", "Int(x)", "Nullable(Int64)");
        yield return (DataType.Integer, "25", "Num(x)", "Nullable(Decimal(18, 10))");
        yield return (DataType.Boolean, "true", "Int(x)", "Nullable(Int64)");
        yield return (DataType.Boolean, "true", "Num(x)", "Nullable(Decimal(18, 10))");
    }

    private static SelectItem Select(string alias, string expression)
    {
        return new SelectItem
        {
            Alias = alias,
            Expression = Expr.Parse(expression).Value
        };
    }
}

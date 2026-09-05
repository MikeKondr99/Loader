using Loader.Lang.Expressions;
using Loader.Query.Compile;
using Loader.Query.Functions;
using Loader.Query.Models;
using Loader.Query.Resolve;
using QueryTemplate = Loader.Query.Template.Template;

namespace Loader.Query.Tests;

public sealed class ClickHouseQueryCompilerTests
{
    [Test]
    [DisplayName("ClickHouseQueryCompiler компилирует resolved query в SELECT")]
    public async Task Compile_writes_resolved_query_like_redatas_sql_compiler()
    {
        var source = new QuerySource
        {
            Sql = "`tmp_orders`",
            Alias = "stage",
            Fields =
            [
                Field("city", DataType.Text),
                Field("amount", DataType.Number)
            ]
        };
        var query = new Query.Models.Query
        {
            Source = source,
            Select =
            [
                new SelectItem { Alias = "city", Expression = Expr.Parse("city").Value },
                new SelectItem { Alias = "total", Expression = Expr.Parse("SUM(amount)").Value }
            ],
            Where = Expr.Parse("amount > 0").Value,
            GroupBy = [Expr.Parse("city").Value],
            OrderBy =
            [
                new OrderItem
                {
                    Expression = Expr.Parse("SUM(amount)").Value,
                    Direction = OrderDirection.Desc
                }
            ],
            Limit = 10,
            Offset = 5
        };
        var resolved = new QueryResolver()
            .Resolve(query, ClickHouseFunctions.CreateResolver())
            .Value!;
        var compiler = new ClickHouseQueryCompiler
        {
            ExpressionCompiler = new ExpressionCompiler()
        };

        var sql = compiler.Compile(resolved);

        await Assert.That(sql).IsEqualTo(string.Join(
            Environment.NewLine,
            "SELECT",
            "    stage.city AS `column1`, ",
            "    COALESCE(SUM(stage.amount), 0) AS `column2`",
            "FROM `tmp_orders` AS stage",
            "WHERE (stage.amount > toFloat64(0))",
            "GROUP BY `column1`",
            "ORDER BY COALESCE(SUM(stage.amount), 0) DESC",
            "LIMIT 10",
            "OFFSET 5"));
    }

    [Test]
    [DisplayName("CORREL для int,int компилируется без лишних cast")]
    public async Task Compile_correl_integer_integer_without_float_cast()
    {
        var sql = CompileCorrel(DataType.Integer, DataType.Integer);

        await Assert.That(sql).IsEqualTo(string.Join(
            Environment.NewLine,
            "SELECT",
            "    CASE WHEN isFinite(corr(stage.x, stage.y)) THEN corr(stage.x, stage.y) ELSE NULL END AS `column1`",
            "FROM `tmp_values` AS stage"));
    }

    [Test]
    [DisplayName("CORREL для num,num кастует аргументы в Float64 для decimal значений")]
    public async Task Compile_correl_number_number_casts_decimal_arguments()
    {
        var sql = CompileCorrel(DataType.Number, DataType.Number);

        await Assert.That(sql).IsEqualTo(string.Join(
            Environment.NewLine,
            "SELECT",
            "    CASE WHEN isFinite(corr(CAST(stage.x AS Nullable(Float64)), CAST(stage.y AS Nullable(Float64)))) THEN corr(CAST(stage.x AS Nullable(Float64)), CAST(stage.y AS Nullable(Float64))) ELSE NULL END AS `column1`",
            "FROM `tmp_values` AS stage"));
    }

    private static string CompileCorrel(DataType leftType, DataType rightType)
    {
        var source = new QuerySource
        {
            Sql = "`tmp_values`",
            Alias = "stage",
            Fields =
            [
                Field("x", leftType),
                Field("y", rightType)
            ]
        };
        var query = new Query.Models.Query
        {
            Source = source,
            Select =
            [
                new SelectItem { Alias = "correlation", Expression = Expr.Parse("CORREL(x, y)").Value }
            ]
        };
        var resolved = new QueryResolver()
            .Resolve(query, ClickHouseFunctions.CreateResolver())
            .Value!;
        var compiler = new ClickHouseQueryCompiler
        {
            ExpressionCompiler = new ExpressionCompiler()
        };

        return compiler.Compile(resolved);
    }

    private static Field Field(string alias, DataType dataType)
    {
        return new Field
        {
            Alias = alias,
            Template = QueryTemplate.Text($"stage.{alias}"),
            Type = new FieldType
            {
                DataType = dataType,
                CanBeNull = false
            }
        };
    }
}

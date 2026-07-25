using System.Globalization;
using Loader.Lang.Expressions;
using Loader.Query.Models;
using Loader.Query.Tests.Infrastructure;
using TUnit.Assertions.Enums;

namespace Loader.Query.Tests;

public sealed class QueryExecutionTests : ClickHouseExpressionTestBase
{
    public QueryExecutionTests(ClickHouseTestDatabase database)
        : base(database)
    {
    }

    [Test]
    [DisplayName("Query выполняет SELECT * из inline source")]
    public async Task Table_query()
    {
        // Arrange
        var query = UsersQuery() with
        {
            OrderBy = ["UserId".Asc()]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows).Count().IsEqualTo(8);
        await Assert.That(rows[0].Int("UserId")).IsEqualTo(1);
        await Assert.That(rows[0].Text("FirstName")).IsEqualTo("John");
    }

    [Test]
    [DisplayName("Query применяет WHERE")]
    public async Task Where_query()
    {
        // Arrange
        var query = UsersQuery() with
        {
            Where = Expr("UserId > 5"),
            OrderBy = ["UserId".Asc()]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Ints("UserId"))
            .IsEquivalentTo([6, 7, 8], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query применяет WHERE и SELECT вместе")]
    public async Task Where_select_combo_query()
    {
        // Arrange
        var query = UsersQuery() with
        {
            Select = ["UserId * 2".As("UserId")],
            Where = Expr("UserId > 5"),
            OrderBy = ["UserId".Asc()]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Ints("UserId"))
            .IsEquivalentTo([12, 14, 16], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query применяет SELECT expressions")]
    public async Task Select_query()
    {
        // Arrange
        var query = UsersQuery() with
        {
            Select =
            [
                "UserId".As("id"),
                "Upper(FirstName + LastName)".As("Name"),
                "2 * Age".As("DoubleAge"),
                "Age".As("Age")
            ],
            OrderBy = ["UserId".Asc()]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows[0].Int("id")).IsEqualTo(1);
        await Assert.That(rows[0].Text("Name")).IsEqualTo("JOHNDOE");
        await Assert.That(rows[0].Int("DoubleAge")).IsEqualTo(50);
        await Assert.That(rows[0].Int("Age")).IsEqualTo(25);
    }

    [Test]
    [DisplayName("Query возвращает boolean SELECT expression как bool после Normalize")]
    public async Task Boolean_select_expression_returns_bool_after_normalize()
    {
        // Arrange
        var query = UsersQuery() with
        {
            Select = ["Age > 30".As("IsOlder")],
            OrderBy = ["UserId".Asc()]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Bools("IsOlder"))
            .IsEquivalentTo([false, false, true, false, true, true, false, true], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query применяет ORDER BY")]
    public async Task Order_by_query()
    {
        // Arrange
        var query = UsersQuery() with
        {
            OrderBy = ["Salary".Desc()]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Numbers("Salary"))
            .IsEquivalentTo(
                [80000.0, 70000.0, 60000.0, 60000.0, 55000.0, 50000.0, 40000.0, 30000.0],
                CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query применяет несколько ORDER BY")]
    public async Task Order_by_multiple_query()
    {
        // Arrange
        var query = UsersQuery() with
        {
            OrderBy =
            [
                "Notes".Asc(),
                "FirstName".Asc()
            ]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Texts("FirstName"))
            .IsEquivalentTo(
                ["Alice", "Diana", "Jane", "John", "Charlie", "Bob", "Eve", "Frank"],
                CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query применяет LIMIT")]
    public async Task Limit_query()
    {
        // Arrange
        var query = UsersQuery() with
        {
            OrderBy = ["UserId".Asc()],
            Limit = 3
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Ints("UserId"))
            .IsEquivalentTo([1, 2, 3], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query применяет OFFSET")]
    public async Task Offset_query()
    {
        // Arrange
        var query = UsersQuery() with
        {
            OrderBy = ["UserId".Asc()],
            Offset = 5
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Ints("UserId"))
            .IsEquivalentTo([6, 7, 8], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query применяет LIMIT и OFFSET")]
    public async Task Limit_offset_query()
    {
        // Arrange
        var query = UsersQuery() with
        {
            OrderBy = ["UserId".Asc()],
            Limit = 2,
            Offset = 3
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Ints("UserId"))
            .IsEquivalentTo([4, 5], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query группирует только по ключу")]
    public async Task Group_by_with_only_group()
    {
        // Arrange
        var query = UsersQuery() with
        {
            Select = ["Notes".As("TEST")],
            GroupBy = [Expr("Notes")]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Texts("TEST").Order().ToArray())
            .IsEquivalentTo(["Active user", "Blocked", "New"], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query группирует с агрегациями")]
    public async Task Group_by_with_aggregations()
    {
        // Arrange
        var query = UsersQuery() with
        {
            Select =
            [
                "Notes".As("TEST"),
                "SUM(Salary)".As("Sum"),
                "AVG(Salary)".As("Avg"),
                "MIN(Salary)".As("Min"),
                "MAX(Salary)".As("Max")
            ],
            GroupBy = [Expr("Notes")],
            OrderBy = ["Notes".Asc()]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        var active = rows[0];
        await Assert.That(active.Text("TEST")).IsEqualTo("Active user");
        await AssertNumberAsync(active["Sum"], 260000.0);
        await AssertNumberAsync(active["Avg"], 65000.0);
        await AssertNumberAsync(active["Min"], 50000.0);
        await AssertNumberAsync(active["Max"], 80000.0);
    }

    [Test]
    [DisplayName("Query группирует по нескольким ключам")]
    public async Task Group_by_multiple_keys()
    {
        // Arrange
        var query = UsersQuery() with
        {
            Select =
            [
                "FirstName".As("FirstName"),
                "LastName".As("LastName"),
                "COUNT()".As("Count"),
                "SUM(Salary)".As("TotalSalary")
            ],
            GroupBy =
            [
                Expr("FirstName"),
                Expr("LastName")
            ],
            OrderBy = ["FirstName".Asc()]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows).Count().IsEqualTo(8);
        await Assert.That(rows[0].Text("FirstName")).IsEqualTo("Alice");
        await AssertNumberAsync(rows[0]["Count"], 1);
        await AssertNumberAsync(rows[0]["TotalSalary"], 70000.0);
    }

    [Test]
    [DisplayName("Query группирует и сортирует по агрегату")]
    public async Task Group_by_with_order_by()
    {
        // Arrange
        var query = UsersQuery() with
        {
            Select =
            [
                "Notes".As("Note"),
                "COUNT()".As("UserCount")
            ],
            GroupBy = [Expr("Notes")],
            OrderBy = ["COUNT()".Desc()]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows[0].Text("Note")).IsEqualTo("Active user");
        await AssertNumberAsync(rows[0]["UserCount"], 4);
    }

    private static Query.Models.Query UsersQuery()
    {
        return new Query.Models.Query
        {
            Source = UsersSource(),
            Select = []
        };
    }

    private static QuerySource UsersSource()
    {
        return InlineQueryArrange.Source(
            [
                new InlineField("UserId", DataType.Integer),
                new InlineField("FirstName", DataType.Text),
                new InlineField("LastName", DataType.Text),
                new InlineField("Age", DataType.Integer),
                new InlineField("Salary", DataType.Number),
                new InlineField("Notes", DataType.Text)
            ],
            [
                ["1", "'John'", "'Doe'", "25", "50000.0", "'Active user'"],
                ["2", "'Jane'", "'Smith'", "30", "60000.0", "'Active user'"],
                ["3", "'Bob'", "'Brown'", "35", "40000.0", "'New'"],
                ["4", "'Alice'", "'White'", "28", "70000.0", "'Active user'"],
                ["5", "'Charlie'", "'Black'", "40", "30000.0", "'Blocked'"],
                ["6", "'Diana'", "'Green'", "32", "80000.0", "'Active user'"],
                ["7", "'Eve'", "'Stone'", "27", "55000.0", "'New'"],
                ["8", "'Frank'", "'Moore'", "45", "60000.0", "'New'"]
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
}

internal static class QueryExecutionTestExtensions
{
    public static SelectItem As(this string expression, string alias)
    {
        return new SelectItem
        {
            Alias = alias,
            Expression = Loader.Lang.Expressions.Expr.Parse(expression).Value
        };
    }

    public static OrderItem Asc(this string expression)
    {
        return Order(expression, OrderDirection.Asc);
    }

    public static OrderItem Desc(this string expression)
    {
        return Order(expression, OrderDirection.Desc);
    }

    public static int Int(this IReadOnlyDictionary<string, object?> row, string name)
    {
        return Convert.ToInt32(row[name], CultureInfo.InvariantCulture);
    }

    public static string Text(this IReadOnlyDictionary<string, object?> row, string name)
    {
        return Convert.ToString(row[name], CultureInfo.InvariantCulture)!;
    }

    public static int[] Ints(this IEnumerable<IReadOnlyDictionary<string, object?>> rows, string name)
    {
        return rows.Select(row => row.Int(name)).ToArray();
    }

    public static double[] Numbers(this IEnumerable<IReadOnlyDictionary<string, object?>> rows, string name)
    {
        return rows.Select(row => Convert.ToDouble(row[name], CultureInfo.InvariantCulture)).ToArray();
    }

    public static string[] Texts(this IEnumerable<IReadOnlyDictionary<string, object?>> rows, string name)
    {
        return rows.Select(row => row.Text(name)).ToArray();
    }

    public static bool[] Bools(this IEnumerable<IReadOnlyDictionary<string, object?>> rows, string name)
    {
        return rows.Select(row => (bool)row[name]!).ToArray();
    }

    private static OrderItem Order(string expression, OrderDirection direction)
    {
        return new OrderItem
        {
            Expression = Loader.Lang.Expressions.Expr.Parse(expression).Value,
            Direction = direction
        };
    }
}

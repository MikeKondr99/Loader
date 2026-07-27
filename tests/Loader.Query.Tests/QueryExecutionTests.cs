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
    [DisplayName("Query не применяет JSON path из колонки потому что ClickHouse требует константу")]
    public async Task Json_path_from_source_field_fails_because_clickhouse_requires_constant_path()
    {
        // Arrange
        var source = InlineQueryArrange.Source(
            [
                new InlineField("Payload", DataType.Text),
                new InlineField("Path", DataType.Text),
                new InlineField("Keep", DataType.Integer)
            ],
            [
                ["'{\"name\":\"Mike\",\"city\":\"Moscow\"}'", "'$.name'", "1"],
                ["'{\"name\":\"Ann\",\"city\":\"Paris\"}'", "'$.name'", "1"],
                ["'{\"name\":\"Ivan\",\"city\":\"Moscow\"}'", "'$.city'", "1"],
                ["'{\"name\":\"Skip\",\"city\":\"Berlin\"}'", "'$.name'", "0"]
            ]);
        var query = new Query.Models.Query
        {
            Source = source,
            Select =
            [
                "Payload.JsonGetText(Path)".As("Value"),
                "COUNT()".As("Count")
            ],
            Where = Expr("Keep = 1"),
            GroupBy = [Expr("Payload.JsonGetText(Path)")],
            OrderBy = ["Payload.JsonGetText(Path)".Asc()]
        };

        // Act
        var act = async () => await GetRowsAsync(query);

        // Assert
        await Assert.That(act)
            .ThrowsExactly<InvalidOperationException>()
            .WithMessage(string.Join(
                Environment.NewLine,
                "Функция 'JsonGetText' требует, чтобы аргумент 2 был константой",
                "Функция 'JsonGetText' требует, чтобы аргумент 2 был константой",
                "Функция 'JsonGetText' требует, чтобы аргумент 2 был константой"));
    }

    [Test]
    [DisplayName("Query разбирает JSON Lines загруженный как одна текстовая колонка")]
    public async Task Query_parses_json_lines_loaded_as_text_column()
    {
        // Arrange
        var source = InlineQueryArrange.Source(
            [
                new InlineField("Line", DataType.Text)
            ],
            [
                ["'{\"id\":1,\"name\":\"Mike\",\"amount\":10.50,\"active\":true}'"],
                ["'{\"id\":2,\"name\":\"Ann\",\"amount\":\"20.25\",\"active\":\"false\"}'"],
                ["'{\"id\":3,\"name\":\"Skip\",\"amount\":\"not-num\",\"active\":true}'"],
                ["'not-json'"]
            ]);
        var query = new Query.Models.Query
        {
            Source = source,
            Select =
            [
                "Line.JsonGetInt('$.id')".As("Id"),
                "Line.JsonGetText('$.name')".As("Name"),
                "Line.JsonGetNum('$.amount')".As("Amount"),
                "Line.JsonGetBool('$.active')".As("Active")
            ],
            Where = Expr("Line.JsonGetInt('$.id').NotNull()"),
            OrderBy = ["Line.JsonGetInt('$.id')".Asc()]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Ints("Id"))
            .IsEquivalentTo([1, 2, 3], CollectionOrdering.Matching);
        await Assert.That(rows.Texts("Name"))
            .IsEquivalentTo(["Mike", "Ann", "Skip"], CollectionOrdering.Matching);
        await Assert.That(rows.Select(static row => row["Amount"] is null ? (double?)null : Convert.ToDouble(row["Amount"], CultureInfo.InvariantCulture)).ToArray())
            .IsEquivalentTo((double?[])[10.50, 20.25, null], CollectionOrdering.Matching);
        await Assert.That(rows.Select(static row => row["Active"]).ToArray())
            .IsEquivalentTo((object?[])[true, false, true], CollectionOrdering.Matching);
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
    [DisplayName("Query возвращает nullable boolean SELECT expression как Nullable(Bool) после Normalize")]
    public async Task Nullable_boolean_select_expression_preserves_null_after_normalize()
    {
        // Arrange
        var source = InlineQueryArrange.Source(
            [
                new InlineField("id", DataType.Integer),
                new InlineField("Age", DataType.Integer, CanBeNull: true)
            ],
            [
                ["1", "25"],
                ["2", "NULL"],
                ["3", "35"]
            ]);
        var query = new Query.Models.Query
        {
            Source = source,
            Select = ["Age > 30".As("IsOlder")],
            OrderBy = ["id".Asc()]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Select(static row => row["IsOlder"]).ToArray())
            .IsEquivalentTo((object?[])[false, null, true], CollectionOrdering.Matching);
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

    [Test]
    [DisplayName("Query применяет WHERE до GROUP BY")]
    public async Task Where_is_applied_before_group_by()
    {
        // Arrange
        var query = UsersQuery() with
        {
            Select =
            [
                "Notes".As("Note"),
                "COUNT()".As("UserCount"),
                "SUM(Salary)".As("TotalSalary")
            ],
            Where = Expr("Age >= 30"),
            GroupBy = [Expr("Notes")],
            OrderBy = ["Notes".Asc()]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Texts("Note"))
            .IsEquivalentTo(["Active user", "Blocked", "New"], CollectionOrdering.Matching);
        await Assert.That(rows.Ints("UserCount"))
            .IsEquivalentTo([2, 1, 2], CollectionOrdering.Matching);
        await Assert.That(rows.Numbers("TotalSalary"))
            .IsEquivalentTo([140000.0, 30000.0, 100000.0], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query применяет GROUP BY ORDER BY LIMIT вместе")]
    public async Task Group_by_order_by_limit_combo()
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
            OrderBy =
            [
                "COUNT()".Desc(),
                "Notes".Asc()
            ],
            Limit = 2
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Texts("Note"))
            .IsEquivalentTo(["Active user", "New"], CollectionOrdering.Matching);
        await Assert.That(rows.Ints("UserCount"))
            .IsEquivalentTo([4, 3], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query сортирует по выражению которого нет в SELECT")]
    public async Task Order_by_expression_does_not_need_to_be_selected()
    {
        // Arrange
        var query = UsersQuery() with
        {
            Select = ["FirstName".As("Name")],
            OrderBy =
            [
                "Age + Salary".Desc(),
                "FirstName".Asc()
            ],
            Limit = 3
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Texts("Name"))
            .IsEquivalentTo(["Diana", "Alice", "Frank"], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query применяет OFFSET без LIMIT после ORDER BY")]
    public async Task Offset_without_limit_is_applied_after_order_by()
    {
        // Arrange
        var query = UsersQuery() with
        {
            Select = ["FirstName".As("Name")],
            OrderBy = ["Salary".Desc()],
            Offset = 6
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Texts("Name"))
            .IsEquivalentTo(["Bob", "Charlie"], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query фильтрует nullable поля без ручной проверки NULL")]
    public async Task Where_on_nullable_field_filters_nulls()
    {
        // Arrange
        var source = InlineQueryArrange.Source(
            [
                new InlineField("id", DataType.Integer),
                new InlineField("score", DataType.Number, CanBeNull: true)
            ],
            [
                ["1", "10.5"],
                ["2", "NULL"],
                ["3", "7.25"],
                ["4", "0.0"]
            ]);
        var query = new Query.Models.Query
        {
            Source = source,
            Select =
            [
                "id".As("id"),
                "score".As("score")
            ],
            Where = Expr("score > 0"),
            OrderBy = ["id".Asc()]
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Ints("id"))
            .IsEquivalentTo([1, 3], CollectionOrdering.Matching);
        await Assert.That(rows.Numbers("score"))
            .IsEquivalentTo([10.5, 7.25], CollectionOrdering.Matching);
    }

    [Test]
    [DisplayName("Query возвращает SELECT * после WHERE ORDER BY LIMIT OFFSET")]
    public async Task Select_all_with_where_order_by_limit_offset()
    {
        // Arrange
        var query = UsersQuery() with
        {
            Where = Expr("Notes != 'Blocked'"),
            OrderBy =
            [
                "Age".Desc(),
                "UserId".Asc()
            ],
            Limit = 3,
            Offset = 1
        };

        // Act
        var rows = await GetRowsAsync(query);

        // Assert
        await Assert.That(rows.Ints("UserId"))
            .IsEquivalentTo([3, 6, 2], CollectionOrdering.Matching);
        await Assert.That(rows.Texts("FirstName"))
            .IsEquivalentTo(["Bob", "Diana", "Jane"], CollectionOrdering.Matching);
        await Assert.That(rows[0].ContainsKey("LastName")).IsTrue();
        await Assert.That(rows[0].ContainsKey("Salary")).IsTrue();
        await Assert.That(rows[0].ContainsKey("Notes")).IsTrue();
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

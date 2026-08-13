using Loader.Script.Tests.Infrastructure;

namespace Loader.Script.Tests;

[TestWithDependency(DatabaseDependency.ClickHouseDwh, DatabaseDependency.Ydb)]
public sealed class LoadStatementYdbTests
{
    private readonly ClickHouseTestDatabase clickHouse;

    public LoadStatementYdbTests(ClickHouseTestDatabase clickHouse)
    {
        this.clickHouse = clickHouse;
    }

    [Test]
    [DisplayName("LOAD из Connect YDB source перегружает данные через temp в final table")]
    public async Task Connect_ydb_load_materializes_expected_final_table()
    {
        // Arrange
        await using var ydb = await YdbTestDatabase.StartAsync();
        var registry = new InMemoryConnectionRegistry(
        [
            new ScriptConnection
            {
                Name = "test_ydb",
                Provider = ScriptConnectionType.Ydb,
                ConnectionString = ydb.ConnectionString
            }
        ]);

        // Act
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            clickHouse,
            """
            ydb_people:
            LOAD
                Text(id) AS id,
                Upper(name) AS name,
                city
            FROM Connect(name='test_ydb')
            SQL
                select id, name, city from as_table([
                    <|id:Int32("1"), name:Utf8("Alice"), city:Utf8("Moscow")|>,
                    <|id:Int32("2"), name:Utf8("Bob"), city:Utf8("Berlin")|>,
                    <|id:Int32("3"), name:Utf8("Charlie"), city:Utf8("London")|>
                ])
                where city != Utf8("Berlin")
                order by id;
            """,
            registry);

        // Assert
        var result = execution.Tables;
        await Assert.That(result).Count().IsEqualTo(1);
        await Assert.That(result[0].Alias).IsEqualTo("ydb_people");
        await ScriptIntegrationAssert.AssertFinalTableAsync(
            clickHouse,
            result[0],
            ["id", "name", "city"],
            [
                ["1", "ALICE", "Moscow"],
                ["3", "CHARLIE", "London"]
            ],
            "ORDER BY `column1` ASC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(clickHouse, execution);
    }

    [Test]
    [DisplayName("LOAD из Connect YDB table читает реальные строки и nullable значения")]
    public async Task Connect_ydb_table_load_materializes_nullable_values()
    {
        // Arrange
        await using var ydb = await YdbTestDatabase.StartAsync();
        var sourceTable = $"script_ydb_orders_{Guid.NewGuid():N}";
        await ydb.ExecuteAsync($"DROP TABLE IF EXISTS {sourceTable};");
        await ydb.ExecuteAsync(
            $"""
            CREATE TABLE {sourceTable}
            (
                id Int32 NOT NULL,
                city Utf8,
                amount Decimal(10, 2),
                active Bool,
                created_at Datetime,
                PRIMARY KEY(id)
            );
            """);
        await ydb.ExecuteAsync(
            $"""
            UPSERT INTO {sourceTable} (id, city, amount, active, created_at) VALUES
            (1, Utf8("Moscow"), Decimal("10.50", 10, 2), Bool("true"), Datetime("2026-01-01T10:00:00Z")),
            (2, Utf8("Berlin"), NULL, NULL, Datetime("2026-01-02T11:00:00Z")),
            (3, Utf8("Moscow"), Decimal("25.75", 10, 2), Bool("false"), Datetime("2026-01-03T12:00:00Z"));
            """);
        var registry = CreateRegistry(ydb);

        // Act
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            clickHouse,
            $$"""
            ydb_orders:
            LOAD
                id,
                city,
                amount,
                active,
                created_at
            FROM Connect(name='test_ydb')
            SQL SELECT id, city, amount, active, created_at FROM {{sourceTable}} ORDER BY id;
            """,
            registry);

        // Assert
        var result = execution.Tables;
        await Assert.That(result).Count().IsEqualTo(1);
        await Assert.That(result[0].Alias).IsEqualTo("ydb_orders");
        await ScriptIntegrationAssert.AssertFinalTableAsync(
            clickHouse,
            result[0],
            ["id", "city", "amount", "active", "created_at"],
            [
                [1, "Moscow", 10.50m, true, new DateTime(2026, 1, 1, 10, 0, 0)],
                [2, "Berlin", null, null, new DateTime(2026, 1, 2, 11, 0, 0)],
                [3, "Moscow", 25.75m, false, new DateTime(2026, 1, 3, 12, 0, 0)]
            ],
            "ORDER BY `column1` ASC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(clickHouse, execution);
    }

    [Test]
    [DisplayName("LOAD из Connect YDB source SQL с агрегацией перегружает агрегированный результат")]
    public async Task Connect_ydb_source_sql_aggregation_materializes_expected_result()
    {
        // Arrange
        await using var ydb = await YdbTestDatabase.StartAsync();
        var sourceTable = $"script_ydb_sales_{Guid.NewGuid():N}";
        await ydb.ExecuteAsync($"DROP TABLE IF EXISTS {sourceTable};");
        await ydb.ExecuteAsync(
            $"""
            CREATE TABLE {sourceTable}
            (
                id Int32 NOT NULL,
                city Utf8,
                amount Decimal(10, 2),
                PRIMARY KEY(id)
            );
            """);
        await ydb.ExecuteAsync(
            $"""
            UPSERT INTO {sourceTable} (id, city, amount) VALUES
            (1, Utf8("Moscow"), Decimal("10.50", 10, 2)),
            (2, Utf8("Berlin"), Decimal("5.25", 10, 2)),
            (3, Utf8("Moscow"), Decimal("25.75", 10, 2));
            """);
        var registry = CreateRegistry(ydb);

        // Act
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            clickHouse,
            $$"""
            ydb_city_sales:
            LOAD
                city,
                cnt,
                total
            FROM Connect(name='test_ydb')
            SQL
                SELECT city, COUNT(*) AS cnt, SUM(amount) AS total
                FROM {{sourceTable}}
                GROUP BY city
                ORDER BY city;
            """,
            registry);

        // Assert
        var result = execution.Tables;
        await Assert.That(result).Count().IsEqualTo(1);
        await ScriptIntegrationAssert.AssertFinalTableAsync(
            clickHouse,
            result[0],
            ["city", "cnt", "total"],
            [
                ["Berlin", 1UL, 5.25m],
                ["Moscow", 2UL, 36.25m]
            ],
            "ORDER BY `column1` ASC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(clickHouse, execution);
    }

    [Test]
    [DisplayName("LOAD из YDB и последующий LOAD FROM LOAD выполняют повторную группировку")]
    public async Task Connect_ydb_load_can_be_used_by_following_load_statement()
    {
        // Arrange
        await using var ydb = await YdbTestDatabase.StartAsync();
        var sourceTable = $"script_ydb_events_{Guid.NewGuid():N}";
        await ydb.ExecuteAsync($"DROP TABLE IF EXISTS {sourceTable};");
        await ydb.ExecuteAsync(
            $"""
            CREATE TABLE {sourceTable}
            (
                id Int32 NOT NULL,
                city Utf8,
                status Utf8,
                amount Decimal(10, 2),
                PRIMARY KEY(id)
            );
            """);
        await ydb.ExecuteAsync(
            $"""
            UPSERT INTO {sourceTable} (id, city, status, amount) VALUES
            (1, Utf8("Moscow"), Utf8("paid"), Decimal("10.50", 10, 2)),
            (2, Utf8("Berlin"), Utf8("draft"), Decimal("5.25", 10, 2)),
            (3, Utf8("Moscow"), Utf8("paid"), Decimal("25.75", 10, 2)),
            (4, Utf8("London"), Utf8("paid"), Decimal("7.00", 10, 2));
            """);
        var registry = CreateRegistry(ydb);

        // Act
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            clickHouse,
            $$"""
            ydb_events:
            LOAD
                city,
                status,
                amount
            FROM Connect(name='test_ydb')
            SQL SELECT city, status, amount FROM {{sourceTable}};

            paid_by_city:
            LOAD
                city,
                SUM(amount) AS total
            FROM ydb_events
            WHERE status = 'paid'
            GROUP BY city
            ORDER BY city;
            """,
            registry);

        // Assert
        var result = execution.Tables;
        await Assert.That(result).Count().IsEqualTo(2);
        await Assert.That(result[0].Alias).IsEqualTo("ydb_events");
        await Assert.That(result[1].Alias).IsEqualTo("paid_by_city");
        await ScriptIntegrationAssert.AssertFinalTableAsync(
            clickHouse,
            result[1],
            ["city", "total"],
            [
                ["London", 7.00m],
                ["Moscow", 36.25m]
            ],
            "ORDER BY `column1` ASC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(clickHouse, execution);
    }

    private static InMemoryConnectionRegistry CreateRegistry(YdbTestDatabase ydb)
    {
        return new InMemoryConnectionRegistry(
        [
            new ScriptConnection
            {
                Name = "test_ydb",
                Provider = ScriptConnectionType.Ydb,
                ConnectionString = ydb.ConnectionString
            }
        ]);
    }
}

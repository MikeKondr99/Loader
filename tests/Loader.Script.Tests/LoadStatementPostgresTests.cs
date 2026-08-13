using Loader.Script.Tests.Infrastructure;

namespace Loader.Script.Tests;

[TestWithDependency(DatabaseDependency.ClickHouseDwh, DatabaseDependency.Postgres)]
public sealed class LoadStatementPostgresTests
{
    private readonly ClickHouseTestDatabase clickHouse;
    private readonly PostgresTestDatabase postgres;

    public LoadStatementPostgresTests(ClickHouseTestDatabase clickHouse, PostgresTestDatabase postgres)
    {
        this.clickHouse = clickHouse;
        this.postgres = postgres;
    }

    [Test]
    [DisplayName("LOAD из Connect Postgres source перегружает данные через temp в final table")]
    public async Task Postgres_load_materializes_expected_final_table()
    {
        // Arrange
        var sourceTable = $"script_pg_source_{Guid.NewGuid():N}";
        await postgres.ExecuteAsync(
            $$"""
            CREATE TABLE public.{{sourceTable}}
            (
                id integer not null,
                name text not null,
                city text not null,
                active boolean not null,
                amount numeric(10, 2) null,
                created_at timestamp not null,
                note text null
            );
            INSERT INTO public.{{sourceTable}} (id, name, city, active, amount, created_at, note) VALUES
            (1, 'Alice', 'Moscow', true, 10.50, timestamp '2024-01-01 10:11:12', 'vip'),
            (2, 'Bob', 'Berlin', false, NULL, timestamp '2024-01-02 11:12:13', NULL),
            (3, 'Charlie', 'London', true, 25.75, timestamp '2024-01-03 12:13:14', 'new');
            """);

        // Act
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            clickHouse,
            $$"""
            pg_people:
            LOAD
                Text(id) AS id,
                Upper(name) AS name,
                city AS Город,
                active,
                amount,
                created_at,
                note
            FROM Connect(name='container_pg')
            SQL SELECT * FROM public.{{sourceTable}} WHERE city != 'Berlin' ORDER BY id ASC;

            pg_people_copy:
            LOAD *
            FROM pg_people
            ORDER BY id ASC;
            """,
            postgres);

        // Assert
        var result = execution.Tables;
        await Assert.That(result).Count().IsEqualTo(2);
        await Assert.That(result[0].Alias).IsEqualTo("pg_people");
        await ScriptIntegrationAssert.AssertFinalTableAsync(
            clickHouse,
            result[1],
            ["id", "name", "Город", "active", "amount", "created_at", "note"],
            [
                ["1", "ALICE", "Moscow", true, 10.50m, new DateTime(2024, 1, 1, 10, 11, 12), "vip"],
                ["3", "CHARLIE", "London", true, 25.75m, new DateTime(2024, 1, 3, 12, 13, 14), "new"]
            ],
            "ORDER BY `column1` ASC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(clickHouse, execution);
    }

    [Test]
    [DisplayName("LOAD из Postgres nullable numeric не падает при записи temp table")]
    public async Task Postgres_load_allows_null_numeric_in_temp_table()
    {
        // Arrange
        var sourceTable = $"script_pg_nullable_source_{Guid.NewGuid():N}";
        await postgres.ExecuteAsync(
            $$"""
            CREATE TABLE public.{{sourceTable}}
            (
                id integer not null,
                amount numeric(10, 2) null
            );
            INSERT INTO public.{{sourceTable}} (id, amount) VALUES
            (1, 10.50),
            (2, NULL),
            (3, 25.75);
            """);

        // Act
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            clickHouse,
            $$"""
            pg_amounts:
            LOAD
                id,
                amount
            FROM Connect(name='container_pg')
            SQL SELECT * FROM public.{{sourceTable}} ORDER BY id ASC;
            """,
            postgres);

        // Assert
        var result = execution.Tables;
        await Assert.That(result).Count().IsEqualTo(1);
        await ScriptIntegrationAssert.AssertFinalTableAsync(
            clickHouse,
            result[0],
            ["id", "amount"],
            [
                [1, 10.50m],
                [2, null],
                [3, 25.75m]
            ],
            "ORDER BY `column1` ASC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(clickHouse, execution);
    }

    [Test]
    [DisplayName("LOAD из Postgres сохраняет final table с физическими columnN при пользовательских alias")]
    public async Task Postgres_load_keeps_final_table_physical_columns_for_user_aliases()
    {
        // Arrange
        var sourceTable = $"script_pg_alias_source_{Guid.NewGuid():N}";
        await postgres.ExecuteAsync(
            $$"""
            CREATE TABLE public.{{sourceTable}}
            (
                city text not null,
                amount numeric(10, 2) not null
            );
            INSERT INTO public.{{sourceTable}} (city, amount) VALUES
            ('Kazan', 830.00),
            ('Moscow', 1250.50),
            ('Spb', 2100.75);
            """);

        // Act
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            clickHouse,
            $$"""
            pg_orders:
            LOAD
                city AS City,
                city AS Город
            FROM Connect(name='container_pg')
            SQL SELECT * FROM public.{{sourceTable}} WHERE amount > 0 ORDER BY city ASC;
            """,
            postgres);

        // Assert
        var result = execution.Tables;
        await Assert.That(result).Count().IsEqualTo(1);
        await ScriptIntegrationAssert.AssertFinalTableAsync(
            clickHouse,
            result[0],
            ["City", "Город"],
            [
                ["Kazan", "Kazan"],
                ["Moscow", "Moscow"],
                ["Spb", "Spb"]
            ],
            "ORDER BY `column1` ASC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(clickHouse, execution);
    }

    [Test]
    [DisplayName("LOAD из результата Postgres LOAD сохраняет Time типы")]
    public async Task Postgres_load_from_previous_load_preserves_time_types()
    {
        // Arrange
        // Act
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            clickHouse,
            """
            pg_time:
            LOAD
              id,
              time_value,
              interval_value
            FROM Connect(name='container_pg')
            SQL
              SELECT
                1 AS id,
                time '03:04:05' AS time_value,
                interval '04:05:06' AS interval_value;

            s:
            LOAD * FROM pg_time;
            """,
            postgres);

        // Assert
        var result = execution.Tables;
        await Assert.That(result).Count().IsEqualTo(2);
        await Assert.That(result[0].Fields[1].DataType).IsEqualTo(Loader.Core.Models.DataType.Time);
        await Assert.That(result[0].Fields[2].DataType).IsEqualTo(Loader.Core.Models.DataType.Time);
        await Assert.That(result[1].Fields[1].DataType).IsEqualTo(Loader.Core.Models.DataType.Time);
        await Assert.That(result[1].Fields[2].DataType).IsEqualTo(Loader.Core.Models.DataType.Time);
        await ScriptIntegrationAssert.AssertFinalTableAsync(
            clickHouse,
            result[1],
            ["id", "time_value", "interval_value"],
            [
                new object?[] { 1, new DateTime(1970, 1, 1, 3, 4, 5), new DateTime(1970, 1, 1, 4, 5, 6) }
            ]);
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(clickHouse, execution);
    }
}

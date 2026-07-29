using Loader.Script.Tests.Infrastructure;

namespace Loader.Script.Tests;

[ClassDataSource<ClickHouseTestDatabase>(Shared = SharedType.PerTestSession)]
[ParallelLimiter<PostgresParallelLimit>]
public sealed class LoadStatementPostgresTests
{
    private readonly ClickHouseTestDatabase clickHouse;

    public LoadStatementPostgresTests(ClickHouseTestDatabase clickHouse)
    {
        this.clickHouse = clickHouse;
    }

    [Test]
    [DisplayName("LOAD из Postgres source перегружает данные через temp в final table")]
    public async Task Postgres_load_materializes_expected_final_table()
    {
        // Arrange
        await using var postgres = await PostgresTestDatabase.StartAsync();
        var sourceTable = $"script_pg_source_{Guid.NewGuid():N}";
        await postgres.ExecuteAsync(
            $$"""
            CREATE TABLE public.{{sourceTable}}
            (
                id integer not null,
                name text not null,
                city text not null
            );
            INSERT INTO public.{{sourceTable}} (id, name, city) VALUES
            (1, 'Alice', 'Moscow'),
            (2, 'Bob', 'Berlin'),
            (3, 'Charlie', 'London');
            """);

        // Act
        var execution = await ScriptIntegrationAssert.ExecuteScriptAsync(
            clickHouse,
            $$"""
            pg_people:
            LOAD
                Text(id) AS id,
                Upper(name) AS name,
                city
            FROM [{{postgres.ConnectionString}}] (postgres, table='public.{{sourceTable}}')
            WHERE city != 'Berlin'
            ORDER BY id ASC;
            """);

        // Assert
        var result = execution.Tables;
        await Assert.That(result).Count().IsEqualTo(1);
        await Assert.That(result[0].Alias).IsEqualTo("pg_people");
        await ScriptIntegrationAssert.AssertFinalTableAsync(
            clickHouse,
            result[0],
            ["id", "name", "city"],
            [
                ["1", "ALICE", "Moscow"],
                ["3", "CHARLIE", "London"]
            ],
            "ORDER BY `id` ASC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(clickHouse, execution);
    }

    [Test]
    [DisplayName("LOAD из Postgres nullable numeric не падает при записи temp table")]
    public async Task Postgres_load_allows_null_numeric_in_temp_table()
    {
        // Arrange
        await using var postgres = await PostgresTestDatabase.StartAsync();
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
            FROM [{{postgres.ConnectionString}}] (postgres, table='public.{{sourceTable}}')
            ORDER BY id ASC;
            """);

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
            "ORDER BY `id` ASC");
        await ScriptIntegrationAssert.AssertNoTempTablesAsync(clickHouse, execution);
    }
}

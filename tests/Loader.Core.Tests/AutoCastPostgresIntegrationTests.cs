using System.Data.Common;
using Loader.Core.Providers;
using Loader.Core.Providers.Postgres;
using Loader.Core.Providers.Sql;
using Loader.Core.Sources;
using Loader.Core.Tests.Infrastructure;

namespace Loader.Core.Tests;

public sealed class AutoCastPostgresIntegrationTests
{
    private static readonly PostgresProvider Provider = new();
    private static PostgresTestDatabase? Database;

    [Before(Class)]
    public static async Task StartDatabase()
    {
        Database = await PostgresTestDatabase.StartAsync();
    }

    [After(Class)]
    public static async Task StopDatabase()
    {
        if (Database is not null)
        {
            await Database.DisposeAsync();
        }
    }

    [Test]
    [DisplayName("AutoCast Postgres analyzer трогает только text колонки и верит типизированным")]
    public async Task Postgres_analyze_then_auto_cast_only_text_columns()
    {
        const string sql =
            """
            select *
            from (
                values
                    (1::integer, '10.50'::text, timestamp '2026-01-02 00:00:00', date '2026-01-02', '2026-01-02'::text),
                    (2::integer, '20.75'::text, timestamp '2026-02-03 00:00:00', date '2026-02-03', '2026-02-03'::text)
            ) as rows(id, amount_text, created_ts, created_date, date_text)
            order by id
            """;

        var schema = await AnalyzeAsync(sql);

        await Assert.That(schema.Fields.Select(field => field.Name).ToArray())
            .IsEquivalentTo(["amount_text", "date_text"]);

        await using var rawReader = await OpenReaderAsync(sql);
        await using var reader = rawReader
            .Normalize()
            .AutoCast(schema);

        await Assert.That(reader).HaveData(
            columns: ["id", "amount_text", "created_ts", "created_date", "date_text"],
            types: [DataType.Integer, DataType.Number, DataType.DateTime, DataType.Date, DataType.Date],
            rows: [
                (1, 10.50m, new DateTime(2026, 1, 2), new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 2)),
                (2, 20.75m, new DateTime(2026, 2, 3), new DateOnly(2026, 2, 3), new DateOnly(2026, 2, 3))
            ]);
    }

    private static async Task<AutoCastSchema> AnalyzeAsync(string sql)
    {
        await using var rawReader = await OpenReaderAsync(sql);
        var analyzer = new AutoCastAnalyzer();
        await using var reader = rawReader
            .Normalize()
            .CollectAutoCast(analyzer);

        while (await reader.ReadAsync())
        {
        }

        return analyzer.Schema ?? throw new InvalidOperationException("AutoCast analyzer did not complete.");
    }

    private static ValueTask<DbDataReader> OpenReaderAsync(string sql)
    {
        var database = Database ?? throw new InvalidOperationException("Postgres test database is not started.");
        return Provider.OpenReaderAsync(
            new ConnectionStringSource
            {
                ConnectionString = database.ConnectionString
            },
            new SqlTableConfig
            {
                Sql = sql
            });
    }
}

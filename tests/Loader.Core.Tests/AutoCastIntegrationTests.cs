using System.Data.Common;
using Loader.Core.Data;
using Loader.Core.Data.AutoCast;
using Loader.Core.Providers.Csv;
using Loader.Core.Providers.Json;
using Loader.Core.Tests.Infrastructure;

namespace Loader.Core.Tests;

public sealed class AutoCastIntegrationTests
{
    private static readonly CsvProvider CsvProvider = new();
    private static readonly JsonProvider JsonProvider = new();

    [Test]
    [DisplayName("AutoCast CSV analyzer schema вторым проходом приводит реальные строковые данные")]
    public async Task Csv_analyze_then_auto_cast()
    {
        var source = new InlineCsv(
            """
            id,city,amount,created_at,shipped_at
            1,Moscow,10.50,2026-01-02,2026-01-02 03:04:05
            2,London,20.75,2026-02-03
            """);
        var config = new CsvTableConfig
        {
            FileName = "orders.csv"
        };

        var schema = await AnalyzeAsync(() => CsvProvider.OpenReaderAsync(source, config));

        await using var rawReader = await CsvProvider.OpenReaderAsync(source, config);
        await using var reader = rawReader
            .Normalize()
            .AutoCast(schema);

        await Assert.That(reader).HaveData(
            columns: ["id", "city", "amount", "created_at", "shipped_at"],
            types: [DataType.Integer, DataType.Text, DataType.Number, DataType.Date, DataType.DateTime],
            rows: [
                (1L, "Moscow", 10.50m, new DateOnly(2026, 1, 2), new DateTime(2026, 1, 2, 3, 4, 5)),
                (2L, "London", 20.75m, new DateOnly(2026, 2, 3), DBNull.Value)
            ]);
    }

    [Test]
    [DisplayName("AutoCast JSON AnalyzeSchema затем analyzer schema приводит flatten данные")]
    public async Task Json_analyze_schema_then_auto_cast()
    {
        var source = new InlineJson(
            """
            {
              "response": {
                "items": [
                  {
                    "id": "1",
                    "city": "Moscow",
                    "customer": { "name": "Mike", "registered": "2026-01-02" },
                    "amount": "10.50",
                    "created_at": "2026-01-02T03:04:05",
                    "discount": "1.25"
                  },
                  {
                    "id": "2",
                    "city": "London",
                    "customer": { "name": "Ann", "registered": "2026-02-03" },
                    "amount": "20.75",
                    "created_at": "2026-02-03T04:05:06"
                  }
                ]
              }
            }
            """);

        var jsonSchema = await JsonProvider.AnalyzeSchemaAsync(source, "orders.json", ["response", "items"]);
        var config = new JsonTableConfig
        {
            FileName = "orders.json",
            ArrayPath = ["response", "items"],
            Schema = jsonSchema
        };
        var autoCastSchema = await AnalyzeAsync(() => JsonProvider.OpenReaderAsync(source, config));

        await using var rawReader = await JsonProvider.OpenReaderAsync(source, config);
        await using var reader = rawReader
            .Normalize()
            .AutoCast(autoCastSchema);

        await Assert.That(reader).HaveData(
            columns: ["id", "city", "customer.name", "customer.registered", "amount", "created_at", "discount"],
            types: [DataType.Integer, DataType.Text, DataType.Text, DataType.Date, DataType.Number, DataType.DateTime, DataType.Number],
            rows: [
                (1L, "Moscow", "Mike", new DateOnly(2026, 1, 2), 10.50m, new DateTime(2026, 1, 2, 3, 4, 5), 1.25m),
                (2L, "London", "Ann", new DateOnly(2026, 2, 3), 20.75m, new DateTime(2026, 2, 3, 4, 5, 6), DBNull.Value)
            ]);
    }

    private static async Task<AutoCastSchema> AnalyzeAsync(Func<ValueTask<DbDataReader>> openReader)
    {
        await using var rawReader = await openReader();
        var analyzer = new AutoCastAnalyzer();
        await using var reader = rawReader
            .Normalize()
            .CollectAutoCast(analyzer);

        while (await reader.ReadAsync())
        {
        }

        return analyzer.Schema ?? throw new InvalidOperationException("AutoCast analyzer did not complete.");
    }
}

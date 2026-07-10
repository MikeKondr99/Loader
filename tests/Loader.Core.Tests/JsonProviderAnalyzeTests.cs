using Loader.Core.Providers.Json;
using Loader.Core.Tests.Infrastructure;

namespace Loader.Core.Tests;

public sealed class JsonProviderAnalyzeTests
{
    private static readonly JsonProvider Provider = new();

    [Test]
    [DisplayName("Json AnalyzeSchema собирает union полей и flatten вложенных объектов")]
    public async Task Analyze_schema_collects_union_of_fields_and_flattens_objects()
    {
        var source = new InlineJson(
            """
            {
              "items": [
                { "id": 1, "user": { "name": "Mike" } },
                { "id": 2, "amount": 10.50 }
              ]
            }
            """);

        var schema = await Provider.AnalyzeSchemaAsync(source, "inline.json", ["items"]);

        await Assert.That(schema.Columns.Select(column => column.Name).ToArray())
            .IsEquivalentTo(["id", "user.name", "amount"]);
    }

    [Test]
    [DisplayName("Json AnalyzeSchema рекурсивно flatten вложенные объекты")]
    public async Task Analyze_schema_recursively_flattens_nested_objects()
    {
        var source = new InlineJson(
            """
            [
              { "id": 1, "user": { "profile": { "city": "Moscow" } } }
            ]
            """);

        var schema = await Provider.AnalyzeSchemaAsync(source, "inline.json", []);

        await Assert.That(schema.Columns.Select(column => column.Name).ToArray())
            .IsEquivalentTo(["id", "user.profile.city"]);
    }

    [Test]
    [DisplayName("Json AnalyzeSchema без flatten оставляет объект одной колонкой")]
    public async Task Analyze_schema_without_flatten_keeps_object_column()
    {
        var source = new InlineJson("""[{ "id": 1, "user": { "name": "Mike" } }]""");

        var schema = await Provider.AnalyzeSchemaAsync(
            source,
            "inline.json",
            [],
            flattenObjects: false);

        await Assert.That(schema.Columns.Select(column => column.Name).ToArray())
            .IsEquivalentTo(["id", "user"]);
    }

    [Test]
    [DisplayName("Json AnalyzeSchema если ArrayPath не указывает на массив кидает provider exception")]
    public async Task Analyze_schema_missing_array_path_throws_provider_exception()
    {
        var source = new InlineJson("""{ "items": { "id": 1 } }""");

        await Assert.That(async () => await Provider.AnalyzeSchemaAsync(source, "inline.json", ["items"]))
            .ThrowsExactly<JsonArrayPathNotFoundProviderException>()
            .WithMessage("JSON file 'inline.json' does not contain an array at path 'items'.");
    }

    [Test]
    [DisplayName("Json AnalyzeSchema находит нужный массив среди мусорных объектов и массивов")]
    public async Task Analyze_schema_reads_deep_array_path_with_noise_around()
    {
        var source = new InlineJson(
            """
            {
              "metadata": {
                "ignored": [
                  { "not": "table" }
                ]
              },
              "response": {
                "debug": {
                  "events": [
                    { "type": "noise" }
                  ]
                },
                "payload": {
                  "wrapper": {
                    "items": [
                      { "id": 1, "city": "Moscow" },
                      { "id": 2, "user": { "name": "Mike" } }
                    ]
                  }
                }
              }
            }
            """);

        var schema = await Provider.AnalyzeSchemaAsync(
            source,
            "inline.json",
            ["response", "payload", "wrapper", "items"]);

        await Assert.That(schema.Columns.Select(column => column.Name).ToArray())
            .IsEquivalentTo(["id", "city", "user.name"]);
    }
}

using Loader.Core.Data;
using Loader.Core.Providers.Json;
using Loader.Core.Tests.Infrastructure;
using System.Text;

namespace Loader.Core.Tests;

public sealed class JsonProviderTests
{
    private static readonly JsonProvider Provider = new();

    [Test]
    [DisplayName("Json с явной схемой читает примитивы строками")]
    public async Task Reads_values_with_explicit_schema_as_strings()
    {
        var source = new InlineJson(
            """
            [
              { "id": 1, "name": "Moscow", "amount": 10.50, "active": true, "created": "2026-01-02" },
              { "id": 2, "name": "London", "amount": 20.00, "active": false, "created": "2026-02-03" }
            ]
            """);

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new JsonTableConfig
            {
                FileName = "inline.json",
                ArrayPath = [],
                Schema = Schema("id", "name", "amount", "active", "created")
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "name", "amount", "active", "created"],
            types: [DataType.Text, DataType.Text, DataType.Text, DataType.Text, DataType.Text],
            rows: [
                ("1", "Moscow", "10.50", "true", "2026-01-02"),
                ("2", "London", "20.00", "false", "2026-02-03")
            ]);
    }

    [Test]
    [DisplayName("Json с ArrayPath читает массив внутри объекта")]
    public async Task Reads_nested_array_path()
    {
        var source = new InlineJson(
            """
            {
              "data": {
                "items": [
                  { "id": 1, "city": "Moscow" },
                  { "id": 2, "city": "London" }
                ]
              }
            }
            """);

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new JsonTableConfig
            {
                FileName = "inline.json",
                ArrayPath = ["data", "items"],
                Schema = Schema("id", "city")
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "city"],
            types: [DataType.Text, DataType.Text],
            rows: [
                ("1", "Moscow"),
                ("2", "London")
            ]);
    }

    [Test]
    [DisplayName("Json с комплексным ArrayPath читает глубокий массив")]
    public async Task Reads_complex_array_path()
    {
        var source = new InlineJson(
            """
            {
              "response": {
                "payload": {
                  "items": [
                    { "id": 1, "city": "Moscow" }
                  ]
                }
              }
            }
            """);

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new JsonTableConfig
            {
                FileName = "inline.json",
                ArrayPath = ["response", "payload", "items"],
                Schema = Schema("id", "city")
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "city"],
            types: [DataType.Text, DataType.Text],
            rows: [
                ("1", "Moscow")
            ]);
    }

    [Test]
    [DisplayName("Json в UTF-16 LE пока не поддерживается и кидает provider exception")]
    public async Task Utf16_little_endian_json_throws_provider_exception()
    {
        var source = new InlineJson(
            """[{ "id": 1, "city": "Москва" }]""",
            Encoding.Unicode);

        await Assert.That(async () => await Provider.OpenReaderAsync(
                source,
                new JsonTableConfig
                {
                    FileName = "inline.json",
                    ArrayPath = [],
                    Schema = Schema("id", "city")
                }))
            .ThrowsExactly<JsonFileOpenProviderException>()
            .WithMessage("JSON file 'inline.json' could not be opened or parsed.");
    }

    [Test]
    [DisplayName("Json отсутствующее поле и null возвращают DBNull")]
    public async Task Missing_field_and_null_return_dbnull()
    {
        var source = new InlineJson(
            """
            [
              { "id": 1, "name": null },
              { "id": 2 }
            ]
            """);

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new JsonTableConfig
            {
                FileName = "inline.json",
                ArrayPath = [],
                Schema = Schema("id", "name")
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "name"],
            types: [DataType.Text, DataType.Text],
            rows: [
                ("1", DBNull.Value),
                ("2", DBNull.Value)
            ]);
    }

    [Test]
    [DisplayName("Json разные примитивы читаются строками")]
    public async Task Reads_different_primitives_as_strings()
    {
        var source = new InlineJson(
            """
            [
              {
                "string": "text",
                "integer": 42,
                "negative": -7,
                "decimal": 10.50,
                "exponent": 1.2e3,
                "true_value": true,
                "false_value": false
              }
            ]
            """);

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new JsonTableConfig
            {
                FileName = "inline.json",
                ArrayPath = [],
                Schema = Schema("string", "integer", "negative", "decimal", "exponent", "true_value", "false_value")
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["string", "integer", "negative", "decimal", "exponent", "true_value", "false_value"],
            types: [DataType.Text, DataType.Text, DataType.Text, DataType.Text, DataType.Text, DataType.Text, DataType.Text],
            rows: [
                ("text", "42", "-7", "10.50", "1.2e3", "true", "false")
            ]);
    }

    [Test]
    [DisplayName("Json nested объект читается по dot-path а массив остается JSON строкой")]
    public async Task Reads_nested_object_by_dot_path_and_array_as_json_text()
    {
        var source = new InlineJson(
            """
            [
              { "id": 1, "user": { "name": "Mike" }, "tags": ["a", "b"] }
            ]
            """);

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new JsonTableConfig
            {
                FileName = "inline.json",
                ArrayPath = [],
                Schema = new JsonTableSchema
                {
                    Columns =
                    [
                        new JsonColumnSchema { Name = "id", Path = "id" },
                        new JsonColumnSchema { Name = "user.name", Path = "user.name" },
                        new JsonColumnSchema { Name = "tags", Path = "tags" }
                    ]
                }
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "user.name", "tags"],
            types: [DataType.Text, DataType.Text, DataType.Text],
            rows: [
                ("1", "Mike", "[\"a\", \"b\"]")
            ]);
    }

    [Test]
    [DisplayName("Json если схема указывает объект то значение возвращается JSON строкой")]
    public async Task Explicit_object_column_returns_json_text()
    {
        var source = new InlineJson("""[{ "id": 1, "user": { "name": "Mike", "age": 30 } }]""");

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new JsonTableConfig
            {
                FileName = "inline.json",
                ArrayPath = [],
                Schema = Schema("id", "user")
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "user"],
            types: [DataType.Text, DataType.Text],
            rows: [
                ("1", "{ \"name\": \"Mike\", \"age\": 30 }")
            ]);
    }

    [Test]
    [DisplayName("Json сначала анализирует схему root массива и потом читает по ней")]
    public async Task Analyze_schema_then_reads_root_array()
    {
        var source = new InlineJson(
            """
            [
              { "id": 1, "user": { "name": "Mike" } },
              { "id": 2, "amount": 10.50 }
            ]
            """);

        var schema = await Provider.AnalyzeSchemaAsync(source, "inline.json", []);
        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new JsonTableConfig
            {
                FileName = "inline.json",
                ArrayPath = [],
                Schema = schema
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "user.name", "amount"],
            types: [DataType.Text, DataType.Text, DataType.Text],
            rows: [
                ("1", "Mike", DBNull.Value),
                ("2", DBNull.Value, "10.50")
            ]);
    }

    [Test]
    [DisplayName("Json сначала анализирует схему nested массива и потом читает по ней")]
    public async Task Analyze_schema_then_reads_nested_array()
    {
        var source = new InlineJson(
            """
            {
              "data": {
                "items": [
                  { "id": 1, "city": "Moscow" },
                  { "id": 2, "city": "London", "meta": { "rank": 10 } }
                ]
              }
            }
            """);

        var schema = await Provider.AnalyzeSchemaAsync(source, "inline.json", ["data", "items"]);
        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new JsonTableConfig
            {
                FileName = "inline.json",
                ArrayPath = ["data", "items"],
                Schema = schema
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "city", "meta.rank"],
            types: [DataType.Text, DataType.Text, DataType.Text],
            rows: [
                ("1", "Moscow", DBNull.Value),
                ("2", "London", "10")
            ]);
    }

    [Test]
    [DisplayName("Json если ArrayPath не указывает на массив кидает provider exception")]
    public async Task Missing_array_path_throws_provider_exception()
    {
        var source = new InlineJson("""{ "items": { "id": 1 } }""");

        await Assert.That(async () => await Provider.OpenReaderAsync(
                source,
                new JsonTableConfig
                {
                    FileName = "inline.json",
                    ArrayPath = ["items"],
                    Schema = Schema("id")
                }))
            .ThrowsExactly<JsonArrayPathNotFoundProviderException>()
            .WithMessage("JSON file 'inline.json' does not contain an array at path 'items'.");
    }

    [Test]
    [DisplayName("Json с битым содержимым кидает JsonFileOpenProviderException")]
    public async Task Malformed_json_throws_provider_exception()
    {
        var source = new InlineJson("""[{ "id": 1 """);

        await Assert.That(async () => await Provider.OpenReaderAsync(
                source,
                new JsonTableConfig
                {
                    FileName = "inline.json",
                    ArrayPath = [],
                    Schema = Schema("id")
                }))
            .ThrowsExactly<JsonFileOpenProviderException>()
            .WithMessage("JSON file 'inline.json' could not be opened or parsed.");
    }

    private static JsonTableSchema Schema(params string[] names)
    {
        return new JsonTableSchema
        {
            Columns = names
                .Select(static name => new JsonColumnSchema
                {
                    Name = name,
                    Path = name
                })
                .ToArray()
        };
    }
}

using Loader.Core.Exceptions;

namespace Loader.Core.Providers.Json;

internal static class JsonTableSchemaValidator
{
    public static void Validate(string fileName, JsonTableSchema schema)
    {
        ThrowIfDuplicate(
            fileName,
            schema.Columns.Select(static column => column.Name),
            "column name");

        ThrowIfDuplicate(
            fileName,
            schema.Columns.Select(static column => column.Path),
            "column path");
    }

    private static void ThrowIfDuplicate(string fileName, IEnumerable<string> values, string valueKind)
    {
        var duplicate = values
            .GroupBy(static value => value, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new JsonInvalidSchemaProviderException(fileName, $"duplicate {valueKind} '{duplicate.Key}'.");
        }
    }
}

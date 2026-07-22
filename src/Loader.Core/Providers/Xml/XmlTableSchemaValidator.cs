using Loader.Core.Exceptions;

namespace Loader.Core.Providers.Xml;

internal static class XmlTableSchemaValidator
{
    public static void Validate(string fileName, XmlTableSchema schema)
    {
        // 1. Имена результата должны однозначно разрешаться через GetOrdinal.
        ThrowIfDuplicate(fileName, schema.Columns.Select(static column => column.Name), "column name");

        // 2. Один XML-атрибут или элемент не должен читаться в несколько колонок.
        ThrowIfDuplicate(fileName, schema.Columns.Select(static column => column.Path), "column path");

        // 3. Flat provider принимает только @attribute или имя прямого дочернего элемента.
        var invalidPath = schema.Columns.FirstOrDefault(static column =>
            column.Path.Length == 0 ||
            column.Path == "@" ||
            column.Path.Contains('/', StringComparison.Ordinal));

        if (invalidPath is not null)
        {
            throw new XmlInvalidSchemaProviderException(
                fileName,
                $"column path '{invalidPath.Path}' is not a flat XML path.");
        }
    }

    private static void ThrowIfDuplicate(string fileName, IEnumerable<string> values, string valueKind)
    {
        var duplicate = values
            .GroupBy(static value => value, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new XmlInvalidSchemaProviderException(fileName, $"duplicate {valueKind} '{duplicate.Key}'.");
        }
    }
}

using System.Data.Common;
using System.Text.Json;
using Loader.Core.Abstractions;
using Loader.Core.Sources;

namespace Loader.Core.Providers.Json;

/// <summary>
/// Provider чтения JSON-массива как таблицы.
/// </summary>
public sealed class JsonProvider : IProvider<IFileSource, JsonTableConfig>
{
    public string Kind => "json";

    public async ValueTask<DbDataReader> OpenReaderAsync(
        IFileSource source,
        JsonTableConfig config,
        CancellationToken cancellationToken = default)
    {
        var document = await OpenDocumentAsync(source, config.FileName, cancellationToken).ConfigureAwait(false);

        // 1. Находим массив, который config объявил таблицей.
        if (!TryGetArray(document.RootElement, config.ArrayPath, out JsonElement array))
        {
            document.Dispose();
            throw new JsonArrayPathNotFoundProviderException(config.FileName, config.ArrayPath);
        }

        // 2. Возвращаем DbDataReader с фиксированной схемой колонок.
        return new JsonProviderDataReader(document, array, config.Schema);
    }

    public async ValueTask<JsonTableSchema> AnalyzeSchemaAsync(
        IFileSource source,
        string fileName,
        IReadOnlyList<string> arrayPath,
        bool flattenObjects = true,
        CancellationToken cancellationToken = default)
    {
        using var document = await OpenDocumentAsync(source, fileName, cancellationToken).ConfigureAwait(false);

        // 1. Находим массив-таблицу.
        if (!TryGetArray(document.RootElement, arrayPath, out var array))
        {
            throw new JsonArrayPathNotFoundProviderException(fileName, arrayPath);
        }

        // 2. Собираем объединение путей по всем объектам массива.
        var columns = new List<JsonColumnSchema>();
        var knownPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in array.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            CollectColumns(row, prefix: string.Empty, flattenObjects, columns, knownPaths);
        }

        return new JsonTableSchema
        {
            Columns = columns
        };
    }

    private static async ValueTask<JsonDocument> OpenDocumentAsync(
        IFileSource source,
        string fileName,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = source.OpenRead(fileName);
            return await JsonDocument
                .ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new JsonFileOpenProviderException(fileName, ex);
        }
    }

    private static bool TryGetArray(JsonElement root, IReadOnlyList<string> arrayPath, out JsonElement array)
    {
        array = root;
        foreach (var segment in arrayPath)
        {
            if (array.ValueKind != JsonValueKind.Object || !array.TryGetProperty(segment, out array))
            {
                return false;
            }
        }

        return array.ValueKind == JsonValueKind.Array;
    }

    private static void CollectColumns(
        JsonElement element,
        string prefix,
        bool flattenObjects,
        List<JsonColumnSchema> columns,
        HashSet<string> knownPaths)
    {
        foreach (var property in element.EnumerateObject())
        {
            var path = prefix.Length == 0 ? property.Name : $"{prefix}.{property.Name}";
            if (flattenObjects && property.Value.ValueKind == JsonValueKind.Object)
            {
                CollectColumns(property.Value, path, flattenObjects, columns, knownPaths);
                continue;
            }

            if (knownPaths.Add(path))
            {
                columns.Add(new JsonColumnSchema
                {
                    Name = path,
                    Path = path
                });
            }
        }
    }
}

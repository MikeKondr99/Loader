using System.Data.Common;
using Loader.Core.Abstractions;
using Loader.Core.Sources;

namespace Loader.Core.Providers.Json;

/// <summary>
/// Provider чтения JSON-массива как таблицы.
/// Провайдер сам выбирает реализацию reader-а по схеме: плоские top-level поля идут через fast reader,
/// dot-path и whole-row поля остаются на совместимом reader-е.
/// </summary>
public sealed class JsonProvider : IProvider<IFileSource, JsonTableConfig>
{
    public string Kind => "json";

    public async ValueTask<DbDataReader> OpenReaderAsync(
        IFileSource source,
        JsonTableConfig config,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Схема должна быть однозначной до открытия файла.
            JsonTableSchemaValidator.Validate(config.FileName, config.Schema);

            // 2. Открываем поток и сразу позиционируем streaming-reader на массиве-таблице.
            var stream = source.OpenRead(config.FileName);
            if (JsonFlatProviderDataReader.CanRead(config.Schema))
            {
                return await JsonFlatProviderDataReader
                    .CreateAsync(stream, config.FileName, config.ArrayPath, config.Schema, cancellationToken)
                    .ConfigureAwait(false);
            }

            return await JsonProviderDataReader
                .CreateAsync(stream, config.FileName, config.ArrayPath, config.Schema, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException
                                   and not JsonArrayPathNotFoundProviderException
                                   and not JsonInvalidSchemaProviderException)
        {
            throw new JsonFileOpenProviderException(config.FileName, ex);
        }
    }

    public async ValueTask<JsonTableSchema> AnalyzeSchemaAsync(
        IFileSource source,
        string fileName,
        IReadOnlyList<string> arrayPath,
        bool flattenObjects = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var stream = source.OpenRead(fileName);
            return await JsonSchemaStreamAnalyzer
                .AnalyzeAsync(stream, fileName, arrayPath, flattenObjects, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not JsonArrayPathNotFoundProviderException)
        {
            throw new JsonFileOpenProviderException(fileName, ex);
        }
    }
}

using System.Data.Common;
using Loader.Core.Abstractions;
using Loader.Core.Sources;
using Sylvan.Data.Csv;

namespace Loader.Core.Providers.Csv;

/// <summary>
/// Provider потокового чтения CSV через Sylvan.Data.Csv.
/// CSV values intentionally stay textual: schema fields are normalized to <c>DataType.Text</c>
/// and values are exposed as strings by <c>DomainDataReader</c>.
/// When <c>HasHeader</c> is false, column names are generated as Excel-style names:
/// <c>A</c>, <c>B</c>, ... <c>Z</c>, <c>AA</c>, <c>AB</c>.
/// Missing row values are left as <c>DBNull</c>. Extra row values beyond the schema are ignored.
/// Empty CSV with required headers is normalized to <see cref="NoHeaderCsvProviderException"/>.
/// Malformed CSV rows are normalized to <see cref="MalformedCsvProviderException"/>.
/// </summary>
public sealed class CsvProvider : IProvider<IFileSource, CsvTableConfig>
{
    public string Kind => "csv";

    public ValueTask<DbDataReader> OpenReaderAsync(
        IFileSource source,
        CsvTableConfig config,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 1. Переносим настройки таблицы в настройки Sylvan CSV reader.
        var readerOptions = new CsvDataReaderOptions
        {
            Delimiter = config.Delimiter,
            HasHeaders = config.HasHeader
        };

        // 2. Открываем файл через source, чтобы provider не знал деталей файловой системы.
        var textReader = source.OpenText(config.FileName, config.Encoding);

        // 3. Создаем потоковый reader и нормализуем provider-level поведение.
        DbDataReader reader;
        try
        {
            reader = CsvDataReader.Create(textReader, readerOptions);
        }
        catch (CsvMissingHeadersException ex)
        {
            throw new NoHeaderCsvProviderException(config.FileName, ex);
        }

        return ValueTask.FromResult<DbDataReader>(
            new CsvProviderDataReader(
                reader,
                config.FileName,
                useGeneratedColumnNames: !config.HasHeader));
    }
}

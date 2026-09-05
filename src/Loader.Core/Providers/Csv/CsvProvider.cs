using System.Data.Common;
using System.Text;
using Loader.Core.Abstractions;
using Loader.Core.Sources;
using Sylvan.Data.Csv;

namespace Loader.Core.Providers.Csv;

/// <summary>
/// Provider потокового чтения CSV через Sylvan.Data.Csv.
/// CSV-значения намеренно остаются текстовыми: поля схемы нормализуются в <c>DataType.Text</c>,
/// а значения отдаются строками через <c>DomainDataReader</c>.
/// Если <c>HasHeader</c> выключен, имена колонок генерируются в Excel-стиле:
/// <c>A</c>, <c>B</c>, ... <c>Z</c>, <c>AA</c>, <c>AB</c>.
/// Пропущенные значения в строке подчиняются тому же правилу nullability, что и пустые значения.
/// Лишние значения сверх схемы игнорируются.
/// Пустой CSV с обязательными заголовками нормализуется в <see cref="NoHeaderCsvProviderException"/>.
/// Некорректные CSV-строки нормализуются в <see cref="MalformedCsvProviderException"/>.
/// Reader использует Sylvan <see cref="CsvStyle.Lax"/>, чтобы принимать распространенные нестрогие CSV:
/// пробелы или текст после закрывающей кавычки и незакрытые кавычки не валят parsing.
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
        if (config.SkipRows < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(config),
                config.SkipRows,
                "SkipRows must be greater than or equal to zero.");
        }

        // 1. Переносим настройки таблицы в настройки Sylvan CSV reader.
        var readerOptions = new CsvDataReaderOptions
        {
            Delimiter = config.Delimiter,
            HasHeaders = config.HasHeader,
            Comment = config.Comment ?? '\0',
            CsvStyle = config.Style
        };

        // 2. Открываем бинарный поток через source, чтобы provider не знал деталей файловой системы.
        var stream = source.OpenRead(config.FileName);
        var textReader = new StreamReader(
            stream,
            config.Encoding ?? Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);
        SkipRows(textReader, config.SkipRows);

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
                useGeneratedColumnNames: !config.HasHeader,
                trimHeaders: config.TrimHeaders,
                trimValues: config.TrimValues,
                emptyAsNull: config.EmptyAsNull));
    }

    private static void SkipRows(TextReader reader, long count)
    {
        for (var index = 0L; index < count; index++)
        {
            if (reader.ReadLine() is null)
            {
                return;
            }
        }
    }
}

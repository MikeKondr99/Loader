using System.Data.Common;
using Sylvan.Data.Csv;

namespace Loader.Demo;

/// <summary>
/// Потоково экспортирует DbDataReader в CSV через Sylvan.Data.Csv.
/// В памяти остается только внутренний буфер writer-а, а не вся итоговая таблица.
/// </summary>
internal sealed class SylvanCsvExporter
{
    public async Task ExportAsync(
        DbDataReader reader,
        Stream outputStream,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1. CsvDataWriter берет схему и имена колонок из DbDataReader.
            var textWriter = new StreamWriter(outputStream);
            await using var writer = CsvDataWriter.Create(textWriter);

            // 2. Передаем reader напрямую writer-у без материализации строк.
            await writer.WriteAsync(reader, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Не удалось экспортировать итоговую таблицу в CSV.", ex);
        }
    }
}

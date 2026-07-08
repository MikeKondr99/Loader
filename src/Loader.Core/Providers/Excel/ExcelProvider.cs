using System.Data.Common;
using Loader.Core.Abstractions;
using Loader.Core.Sources;
using Sylvan.Data.Excel;

namespace Loader.Core.Providers.Excel;

/// <summary>
/// Provider потокового чтения Excel-файлов через Sylvan.Data.Excel.
/// </summary>
public sealed class ExcelProvider : IProvider<IFileSource, ExcelTableConfig>
{
    public string Kind => "excel";

    public async ValueTask<DbDataReader> OpenReaderAsync(
        IFileSource source,
        ExcelTableConfig config,
        CancellationToken cancellationToken = default)
    {
        // 1. Открываем бинарный поток через source, чтобы provider не зависел от файловой системы.
        Stream stream;
        try
        {
            stream = source.OpenRead(config.FileName);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ExcelWorkbookOpenProviderException(config.FileName, ex);
        }

        ExcelWorkbookType workbookType;
        try
        {
            workbookType = ExcelDataReader.GetWorkbookType(config.FileName);
        }
        catch (Exception ex)
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            throw new ExcelWorkbookOpenProviderException(config.FileName, ex);
        }

        // 2. Переносим настройки таблицы в настройки Sylvan Excel reader.
        var readerOptions = new ExcelDataReaderOptions
        {
            Schema = config.HasHeader ? ExcelSchema.Default : ExcelSchema.NoHeaders,
            IgnoreEmptyTrailingRows = config.IgnoreEmptyTrailingRows,
            ReadHiddenWorksheets = config.ReadHiddenWorksheets,
            ReadHiddenRows = config.ReadHiddenRows,
            FormulaErrorHandling = MapFormulaErrorHandling(config.FormulaErrorHandling),
            OwnsStream = true
        };

        // 3. Открываем workbook как потоковый DbDataReader.
        ExcelDataReader reader;
        try
        {
            reader = await ExcelDataReader
                .CreateAsync(stream, workbookType, readerOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            throw new ExcelWorkbookOpenProviderException(config.FileName, ex);
        }

        // 4. Если указан лист, переключаем reader на него и валидируем имя.
        if (!string.IsNullOrWhiteSpace(config.WorksheetName) &&
            !reader.TryOpenWorksheet(config.WorksheetName))
        {
            var worksheets = reader.WorksheetNames.ToArray();
            reader.Dispose();
            throw new ExcelWorksheetNotFoundProviderException(
                config.FileName,
                config.WorksheetName,
                worksheets);
        }

        // 5. Возвращаем reader вызывающему коду, не читая строки заранее.
        return reader;
    }

    private static FormulaErrorHandling MapFormulaErrorHandling(ExcelFormulaErrorMode value)
    {
        return value switch
        {
            ExcelFormulaErrorMode.Exception => FormulaErrorHandling.Exception,
            ExcelFormulaErrorMode.Null => FormulaErrorHandling.Null,
            ExcelFormulaErrorMode.String => FormulaErrorHandling.String,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }
}

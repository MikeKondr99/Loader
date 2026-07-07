using System.Data.Common;
using Loader.Core.Abstractions;
using Loader.Core.Sources;
using Sylvan.Data.Excel;

namespace Loader.Core.Providers.Excel;

/// <summary>
/// Provider потокового чтения Excel-файлов через Sylvan.Data.Excel.
/// </summary>
public sealed class ExcelProvider : IProvider<IPhysicalFileSource, ExcelTableConfig>
{
    public string Kind => "excel";

    public async ValueTask<DbDataReader> OpenReaderAsync(
        IPhysicalFileSource source,
        ExcelTableConfig config,
        CancellationToken cancellationToken = default)
    {
        // 1. Разрешаем путь через source, чтобы provider работал только с допустимым файлом.
        var fullPath = source.ResolveFilePath(config.FileName);

        // 2. Переносим настройки таблицы в настройки Sylvan Excel reader.
        var readerOptions = new ExcelDataReaderOptions
        {
            Schema = config.HasHeader ? ExcelSchema.Default : ExcelSchema.NoHeaders,
            IgnoreEmptyTrailingRows = config.IgnoreEmptyTrailingRows,
            ReadHiddenWorksheets = config.ReadHiddenWorksheets,
            ReadHiddenRows = config.ReadHiddenRows,
            FormulaErrorHandling = MapFormulaErrorHandling(config.FormulaErrorHandling)
        };

        // 3. Открываем workbook как потоковый DbDataReader.
        var reader = await ExcelDataReader
            .CreateAsync(fullPath, readerOptions, cancellationToken)
            .ConfigureAwait(false);

        // 4. Если указан лист, переключаем reader на него и валидируем имя.
        if (!string.IsNullOrWhiteSpace(config.WorksheetName) &&
            !reader.TryOpenWorksheet(config.WorksheetName))
        {
            var worksheets = string.Join(", ", reader.WorksheetNames);
            reader.Dispose();
            throw new InvalidOperationException(
                $"Worksheet '{config.WorksheetName}' was not found in '{config.FileName}'. Available worksheets: {worksheets}.");
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

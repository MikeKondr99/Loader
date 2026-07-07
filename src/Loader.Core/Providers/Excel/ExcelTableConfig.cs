using Loader.Core.Abstractions;

namespace Loader.Core.Providers.Excel;

/// <summary>
/// Настройки чтения одного листа Excel из файлового source.
/// </summary>
public sealed record ExcelTableConfig : ITableConfig
{
    public required string FileName { get; init; }

    public string? WorksheetName { get; init; }

    public bool HasHeader { get; init; } = true;

    public bool IgnoreEmptyTrailingRows { get; init; } = true;

    public bool ReadHiddenWorksheets { get; init; }

    public bool ReadHiddenRows { get; init; } = true;

    public ExcelFormulaErrorMode FormulaErrorHandling { get; init; } = ExcelFormulaErrorMode.Exception;
}

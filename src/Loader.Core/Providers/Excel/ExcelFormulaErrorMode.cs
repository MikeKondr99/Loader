namespace Loader.Core.Providers.Excel;

/// <summary>
/// Политика обработки ошибок формул при чтении Excel.
/// </summary>
public enum ExcelFormulaErrorMode
{
    Exception,
    Null,
    String
}

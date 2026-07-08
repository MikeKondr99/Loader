using Loader.Core.Providers;

namespace Loader.Core.Providers.Excel;

/// <summary>
/// Thrown when Excel provider cannot open a workbook stream as a supported Excel file.
/// </summary>
public sealed class ExcelWorkbookOpenProviderException : FileProviderException
{
    public ExcelWorkbookOpenProviderException(string fileName, Exception innerException)
        : base("excel", fileName, $"Excel file '{fileName}' could not be opened as a supported workbook.", innerException)
    {
    }
}

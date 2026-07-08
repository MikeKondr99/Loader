using Loader.Core.Providers;

namespace Loader.Core.Providers.Excel;

/// <summary>
/// Thrown when an Excel worksheet configured by name does not exist in the workbook.
/// </summary>
public sealed class ExcelWorksheetNotFoundProviderException : FileProviderException
{
    public ExcelWorksheetNotFoundProviderException(
        string fileName,
        string worksheetName,
        IReadOnlyCollection<string> availableWorksheets)
        : base(
            "excel",
            fileName,
            $"Worksheet '{worksheetName}' was not found in Excel file '{fileName}'. Available worksheets: {string.Join(", ", availableWorksheets)}.")
    {
        WorksheetName = worksheetName;
        AvailableWorksheets = availableWorksheets;
    }

    public string WorksheetName { get; }

    public IReadOnlyCollection<string> AvailableWorksheets { get; }
}

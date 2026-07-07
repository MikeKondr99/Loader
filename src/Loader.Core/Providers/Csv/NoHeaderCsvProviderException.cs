using Loader.Core.Providers;

namespace Loader.Core.Providers.Csv;

/// <summary>
/// Thrown when CSV reading expects a header row, but source has no header data.
/// </summary>
public sealed class NoHeaderCsvProviderException : ProviderException
{
    public NoHeaderCsvProviderException(string fileName, Exception innerException)
        : base($"CSV file '{fileName}' does not contain a header row.", innerException)
    {
        FileName = fileName;
    }

    public string FileName { get; }
}

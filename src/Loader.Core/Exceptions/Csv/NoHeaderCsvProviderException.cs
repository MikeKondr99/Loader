using Loader.Core.Providers;

namespace Loader.Core.Exceptions;

/// <summary>
/// Thrown when CSV reading expects a header row, but source has no header data.
/// </summary>
public sealed class NoHeaderCsvProviderException : FileProviderException
{
    public NoHeaderCsvProviderException(string fileName, Exception innerException)
        : base("csv", fileName, $"CSV file '{fileName}' does not contain a header row.", innerException)
    {
    }
}

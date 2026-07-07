using Loader.Core.Providers;

namespace Loader.Core.Providers.Csv;

/// <summary>
/// Thrown when CSV content is malformed and cannot be read as a valid stream.
/// </summary>
public sealed class MalformedCsvProviderException : ProviderException
{
    public MalformedCsvProviderException(string fileName, Exception innerException)
        : base($"CSV file '{fileName}' is malformed.", innerException)
    {
        FileName = fileName;
    }

    public string FileName { get; }
}

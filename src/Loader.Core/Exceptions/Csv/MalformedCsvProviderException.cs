using Loader.Core.Providers;

namespace Loader.Core.Exceptions;

/// <summary>
/// Thrown when CSV content is malformed and cannot be read as a valid stream.
/// </summary>
public sealed class MalformedCsvProviderException : FileProviderException
{
    public MalformedCsvProviderException(string fileName, Exception innerException)
        : base("csv", fileName, $"CSV file '{fileName}' is malformed.", innerException)
    {
    }
}

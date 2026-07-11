namespace Loader.Core.Exceptions;

/// <summary>
/// Base exception for file provider failures before row value reading starts.
/// </summary>
public abstract class FileProviderException : ProviderException
{
    protected FileProviderException(string providerKind, string fileName, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ProviderKind = providerKind;
        FileName = fileName;
    }

    public string ProviderKind { get; }

    public string FileName { get; }
}

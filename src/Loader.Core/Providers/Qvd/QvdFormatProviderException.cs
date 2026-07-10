namespace Loader.Core.Providers.Qvd;

/// <summary>
/// Ошибка структуры или формата QVD-файла.
/// </summary>
public sealed class QvdFormatProviderException : FileProviderException
{
    public QvdFormatProviderException(string fileName, string message, Exception? innerException = null)
        : base("qvd", fileName, $"Could not read QVD file '{fileName}'. {message}", innerException)
    {
    }
}

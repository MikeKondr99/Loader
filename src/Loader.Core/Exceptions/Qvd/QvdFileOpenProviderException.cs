namespace Loader.Core.Exceptions;

/// <summary>
/// Ошибка открытия QVD-файла через файловый source.
/// </summary>
public sealed class QvdFileOpenProviderException : FileProviderException
{
    public QvdFileOpenProviderException(string fileName, Exception innerException)
        : base("qvd", fileName, $"Could not open QVD file '{fileName}'.", innerException)
    {
    }
}

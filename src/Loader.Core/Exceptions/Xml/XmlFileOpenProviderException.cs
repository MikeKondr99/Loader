namespace Loader.Core.Exceptions;

/// <summary>
/// Ошибка открытия или разбора XML-файла.
/// </summary>
public sealed class XmlFileOpenProviderException : FileProviderException
{
    public XmlFileOpenProviderException(string fileName, Exception innerException)
        : base("xml", fileName, $"XML file '{fileName}' could not be opened or parsed.", innerException)
    {
    }
}

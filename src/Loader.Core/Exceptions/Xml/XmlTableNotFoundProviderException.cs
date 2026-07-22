namespace Loader.Core.Exceptions;

/// <summary>
/// XML-файл не содержит элементов выбранной таблицы.
/// </summary>
public sealed class XmlTableNotFoundProviderException : FileProviderException
{
    public XmlTableNotFoundProviderException(string fileName, string tableName)
        : base("xml", fileName, $"XML file '{fileName}' does not contain table element '{tableName}'.")
    {
    }
}

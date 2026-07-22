namespace Loader.Core.Exceptions;

/// <summary>
/// Схема XML-таблицы неоднозначна или содержит неподдерживаемый путь.
/// </summary>
public sealed class XmlInvalidSchemaProviderException : FileProviderException
{
    public XmlInvalidSchemaProviderException(string fileName, string reason)
        : base("xml", fileName, $"XML file '{fileName}' has invalid schema: {reason}")
    {
    }
}

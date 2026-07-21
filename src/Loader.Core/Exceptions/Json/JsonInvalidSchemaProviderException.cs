using Loader.Core.Providers;

namespace Loader.Core.Exceptions;

/// <summary>
/// Ошибка явной JSON-схемы, переданной пользователем.
/// </summary>
public sealed class JsonInvalidSchemaProviderException : FileProviderException
{
    public JsonInvalidSchemaProviderException(string fileName, string reason)
        : base("json", fileName, $"JSON file '{fileName}' has invalid schema: {reason}")
    {
        Reason = reason;
    }

    public string Reason { get; }
}

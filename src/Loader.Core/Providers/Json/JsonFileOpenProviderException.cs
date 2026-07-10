using Loader.Core.Providers;

namespace Loader.Core.Providers.Json;

/// <summary>
/// Ошибка открытия или разбора JSON-файла.
/// </summary>
public sealed class JsonFileOpenProviderException : FileProviderException
{
    public JsonFileOpenProviderException(string fileName, Exception innerException)
        : base("json", fileName, $"JSON file '{fileName}' could not be opened or parsed.", innerException)
    {
    }
}

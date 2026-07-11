using Loader.Core.Providers;

namespace Loader.Core.Exceptions;

/// <summary>
/// Ошибка поиска массива, который должен быть прочитан как таблица.
/// </summary>
public sealed class JsonArrayPathNotFoundProviderException : FileProviderException
{
    public JsonArrayPathNotFoundProviderException(string fileName, IReadOnlyList<string> arrayPath)
        : base("json", fileName, $"JSON file '{fileName}' does not contain an array at path '{FormatPath(arrayPath)}'.")
    {
        ArrayPath = arrayPath;
    }

    public IReadOnlyList<string> ArrayPath { get; }

    private static string FormatPath(IReadOnlyList<string> arrayPath)
    {
        return arrayPath.Count == 0 ? "<root>" : string.Join(".", arrayPath);
    }
}

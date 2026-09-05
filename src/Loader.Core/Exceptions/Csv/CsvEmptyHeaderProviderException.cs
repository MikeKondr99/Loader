using Loader.Core.Providers;

namespace Loader.Core.Exceptions;

/// <summary>
/// Ошибка пустого имени колонки в CSV header после provider-level нормализации.
/// </summary>
public sealed class CsvEmptyHeaderProviderException : FileProviderException
{
    public CsvEmptyHeaderProviderException(string fileName, int ordinal, string? previousColumnName)
        : base("csv", fileName, CreateMessage(fileName, ordinal, previousColumnName))
    {
        Ordinal = ordinal;
        PreviousColumnName = previousColumnName;
    }

    public int Ordinal { get; }

    public string? PreviousColumnName { get; }

    private static string CreateMessage(string fileName, int ordinal, string? previousColumnName)
    {
        var previous = previousColumnName is null
            ? "предыдущей колонки нет"
            : $"предыдущая колонка '{previousColumnName}'";

        return $"CSV файл '{fileName}' содержит пустое имя колонки в header на позиции {ordinal}: {previous}.";
    }
}

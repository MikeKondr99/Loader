namespace Loader.Core.Data;

/// <summary>
/// Ошибка схемы, когда reader возвращает несколько колонок с одинаковым именем.
/// </summary>
public sealed class DuplicateDataFieldNameException : Exception
{
    public DuplicateDataFieldNameException(string name)
        : base($"Column name '{name}' is duplicated.")
    {
    }
}

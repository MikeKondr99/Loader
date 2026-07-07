namespace Loader.Core.Data;

/// <summary>
/// Ошибка чтения или нормализации значения из текущей строки DbDataReader.
/// </summary>
public sealed class DataReaderValueException : Exception
{
    public DataReaderValueException(string fieldName, int ordinal, Exception innerException)
        : base($"Failed to read field '{fieldName}' at ordinal {ordinal}.", innerException)
    {
    }
}

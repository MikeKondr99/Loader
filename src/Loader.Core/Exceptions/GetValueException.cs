namespace Loader.Core.Exceptions;

/// <summary>
/// Base exception for failures while reading or normalizing a value from DbDataReader.
/// </summary>
public abstract class GetValueException : Exception
{
    protected GetValueException(string fieldName, int ordinal, string message, Exception innerException)
        : base(message, innerException)
    {
        FieldName = fieldName;
        Ordinal = ordinal;
    }

    public string FieldName { get; }

    public int Ordinal { get; }
}

namespace Loader.Core.Exceptions;

public sealed class UnknownClrTypeException : NotSupportedException
{
    public UnknownClrTypeException(Type clrType)
        : base($"CLR type '{clrType.FullName}' is unknown to Loader data type mapper.")
    {
        ClrType = clrType;
    }

    public Type ClrType { get; }
}

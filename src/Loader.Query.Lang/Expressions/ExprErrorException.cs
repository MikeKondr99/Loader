namespace Loader.Query.Lang.Expressions;

internal sealed class ExprErrorException : Exception
{
    public ExprErrorException(Exception innerException)
        : base(string.Empty, innerException)
    {
    }

    public required ExprError Error { get; init; }
}

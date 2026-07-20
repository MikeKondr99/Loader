namespace Loader.Lang;

internal sealed class LangErrorException : Exception
{
    public LangErrorException(Exception innerException)
        : base(string.Empty, innerException)
    {
    }

    public required LangError Error { get; init; }
}

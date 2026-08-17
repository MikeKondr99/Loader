namespace Loader.Core.Providers.Jdbc;

public sealed class JdbcProviderException : Exception
{
    public JdbcProviderException(string message)
        : base(message)
    {
    }

    public JdbcProviderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

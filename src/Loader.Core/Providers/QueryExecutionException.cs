namespace Loader.Core.Providers;

/// <summary>
/// Base exception for failures while executing a provider query.
/// </summary>
public abstract class QueryExecutionException : ProviderException
{
    protected QueryExecutionException(string providerKind, string query, string message, Exception innerException)
        : base(message, innerException)
    {
        ProviderKind = providerKind;
        Query = query;
    }

    public string ProviderKind { get; }

    public string Query { get; }
}

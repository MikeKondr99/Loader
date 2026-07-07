namespace Loader.Core.Providers;

/// <summary>
/// Ошибка выполнения запроса через DB provider.
/// </summary>
public sealed class DbExecutionException : ProviderException
{
    public DbExecutionException(string providerKind, string sql, Exception innerException)
        : base($"Database query failed for provider '{providerKind}': {sql}", innerException)
    {
        ProviderKind = providerKind;
        Sql = sql;
    }

    public string ProviderKind { get; }

    public string Sql { get; }
}

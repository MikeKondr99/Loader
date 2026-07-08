namespace Loader.Core.Providers;

/// <summary>
/// Ошибка выполнения запроса через DB provider.
/// </summary>
public sealed class DbExecutionException : QueryExecutionException
{
    public DbExecutionException(string providerKind, string sql, Exception innerException)
        : base(providerKind, sql, $"Database query failed for provider '{providerKind}': {sql}", innerException)
    {
        Sql = sql;
    }

    public string Sql { get; }
}

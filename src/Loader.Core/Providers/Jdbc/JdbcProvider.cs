using System.Data.Common;
using Loader.Core.Abstractions;
using Loader.Core.Providers.Sql;
using Loader.Core.Sources;

namespace Loader.Core.Providers.Jdbc;

/// <summary>
/// Тестовый provider потокового чтения SQL-запросов через JDBC jar, загруженный IKVM.
/// </summary>
public sealed class JdbcProvider : IProvider<IDatabaseSource, SqlTableConfig>
{
    public string Kind => "jdbc";

    public ValueTask<DbDataReader> OpenReaderAsync(
        IDatabaseSource source,
        SqlTableConfig config,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var options = JdbcConnectionOptions.Parse(source.ConnectionString);
        try
        {
            var loader = JdbcDriverLoader.Create(options.JarPaths);
            var driver = JdbcDriverLoader.CreateDriver(loader, options.DriverClass);
            var properties = new java.util.Properties();
            if (!string.IsNullOrWhiteSpace(options.User))
            {
                properties.setProperty("user", options.User);
            }

            if (options.Password is not null)
            {
                properties.setProperty("password", options.Password);
            }

            var connection = driver.connect(options.JdbcUrl, properties)
                             ?? throw new JdbcProviderException(
                                 $"JDBC driver '{options.DriverClass}' не принял url '{options.JdbcUrl}'.");
            var statement = connection.createStatement();
            var resultSet = statement.executeQuery(config.Sql);
            return ValueTask.FromResult<DbDataReader>(new JdbcDataReader(connection, statement, resultSet));
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not DbExecutionException)
        {
            throw new DbExecutionException(Kind, config.Sql, ex);
        }
    }
}

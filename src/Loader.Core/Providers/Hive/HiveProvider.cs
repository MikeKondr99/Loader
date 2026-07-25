using System.Data;
using System.Data.Common;
using System.Data.Odbc;
using Loader.Core.Abstractions;
using Loader.Core.Providers.Sql;
using Loader.Core.Sources;

namespace Loader.Core.Providers.Hive;

/// <summary>
/// Provider потокового чтения SQL-запросов из Apache Hive через ODBC.
/// </summary>
public sealed class HiveProvider : IProvider<IDatabaseSource, SqlTableConfig>
{
    public string Kind => "hive";

    public async ValueTask<DbDataReader> OpenReaderAsync(
        IDatabaseSource source,
        SqlTableConfig config,
        CancellationToken cancellationToken = default)
    {
        var connection = new OdbcConnection(source.ConnectionString);

        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var command = connection.CreateCommand();
            command.CommandText = config.Sql;

            return await command
                .ExecuteReaderAsync(CommandBehavior.SequentialAccess | CommandBehavior.CloseConnection, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw new DbExecutionException(Kind, config.Sql, ex);
        }
    }
}
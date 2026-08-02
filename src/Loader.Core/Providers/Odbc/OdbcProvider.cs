using System.Data;
using System.Data.Common;
using System.Data.Odbc;
using Loader.Core.Abstractions;
using Loader.Core.Providers.Sql;
using Loader.Core.Sources;

namespace Loader.Core.Providers.Odbc;

/// <summary>
/// Provider for streaming SQL query results from a generic ODBC data source.
/// </summary>
public sealed class OdbcProvider : IProvider<IDatabaseSource, SqlTableConfig>
{
    public string Kind => "odbc";

    public async ValueTask<DbDataReader> OpenReaderAsync(
        IDatabaseSource source,
        SqlTableConfig config,
        CancellationToken cancellationToken = default)
    {
        var driverInfo = OdbcDriverDetector.FromConnectionString(source.ConnectionString);
        var connection = new OdbcConnection(source.ConnectionString);

        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            driverInfo = OdbcDriverDetector.FromDriverName(connection.Driver);

            var command = connection.CreateCommand();
            command.CommandText = config.Sql;

            var reader = await command
                .ExecuteReaderAsync(CommandBehavior.SequentialAccess | CommandBehavior.CloseConnection, cancellationToken)
                .ConfigureAwait(false);
            return new OdbcTemporalDataReader(reader);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await connection.DisposeAsync().ConfigureAwait(false);

            var exception = new DbExecutionException(Kind, config.Sql, ErrorMessage(config.Sql, driverInfo), ex);
            exception.Data["OdbcDriverKind"] = driverInfo.Kind.ToString();
            exception.Data["OdbcDriverName"] = driverInfo.Name;
            throw exception;
        }
    }

    private static string ErrorMessage(string sql, OdbcDriverInfo driverInfo)
    {
        return string.IsNullOrWhiteSpace(driverInfo.Name)
            ? $"Database query failed for provider 'odbc' using unknown ODBC driver: {sql}"
            : $"Database query failed for provider 'odbc' using ODBC driver '{driverInfo.Name}' ({driverInfo.Kind}): {sql}";
    }
}
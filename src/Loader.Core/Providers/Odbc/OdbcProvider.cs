using System.Data;
using System.Data.Common;
using System.Data.Odbc;
using System.Diagnostics;
using Loader.Core.Abstractions;
using Loader.Core.Providers.Sql;
using Loader.Core.Sources;

namespace Loader.Core.Providers.Odbc;

/// <summary>
/// Provider потокового чтения SQL-запросов из generic ODBC data source.
/// </summary>
/// <remarks>
/// Provider использует установленный ODBC-драйвер, выбранный connection string.
/// Распознанные имена драйверов используются только для диагностики.
/// </remarks>
public sealed class OdbcProvider : IProvider<IDatabaseSource, SqlTableConfig>
{
    private static readonly ActivitySource ActivitySource = new("Loader.Core.Odbc");

    /// <summary>
    /// Provider marker, используемый в скриптах и диагностике.
    /// </summary>
    public string Kind => "odbc";

    /// <summary>
    /// Открывает ODBC-соединение, выполняет настроенный SQL и возвращает потоковый reader.
    /// </summary>
    /// <param name="source">Database source с ODBC connection string.</param>
    /// <param name="config">SQL-запрос для выполнения.</param>
    /// <param name="cancellationToken">Cancellation token для открытия соединения и выполнения запроса.</param>
    /// <returns>Объект чтения, освобождение которого закрывает ODBC-соединение.</returns>
    public async ValueTask<DbDataReader> OpenReaderAsync(
        IDatabaseSource source,
        SqlTableConfig config,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("OdbcProvider.OpenReader");
        activity?.SetTag("db.system", "odbc");
        activity?.SetTag("db.query.text", config.Sql);

        var driverInfo = OdbcDriverDetector.FromConnectionString(source.ConnectionString);
        SetDriverTags(activity, driverInfo);

        var connection = new OdbcConnection(source.ConnectionString);

        try
        {
            using (ActivitySource.StartActivity("OdbcProvider.ConnectionOpen"))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            driverInfo = OdbcDriverDetector.FromDriverName(connection.Driver);
            SetDriverTags(activity, driverInfo);

            var command = connection.CreateCommand();
            command.CommandText = config.Sql;

            DbDataReader reader;
            using (ActivitySource.StartActivity("OdbcProvider.ExecuteReader"))
            {
                reader = await command
                    .ExecuteReaderAsync(CommandBehavior.SequentialAccess | CommandBehavior.CloseConnection, cancellationToken)
                    .ConfigureAwait(false);
            }

            using (ActivitySource.StartActivity("OdbcProvider.WrapReader"))
            {
                return new OdbcTemporalDataReader(reader);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.AddException(ex);
            await connection.DisposeAsync().ConfigureAwait(false);

            var exception = new DbExecutionException(Kind, config.Sql, ErrorMessage(config.Sql, driverInfo), ex);
            exception.Data["OdbcDriverKind"] = driverInfo.Kind.ToString();
            exception.Data["OdbcDriverName"] = driverInfo.Name;
            throw exception;
        }
    }

    private static void SetDriverTags(Activity? activity, OdbcDriverInfo driverInfo)
    {
        activity?
            .SetTag("odbc.driver.name", driverInfo.Name)
            .SetTag("odbc.driver.kind", driverInfo.Kind.ToString());
    }

    private static string ErrorMessage(string sql, OdbcDriverInfo driverInfo)
    {
        return string.IsNullOrWhiteSpace(driverInfo.Name)
            ? $"Database query failed for provider 'odbc' using unknown ODBC driver: {sql}"
            : $"Database query failed for provider 'odbc' using ODBC driver '{driverInfo.Name}' ({driverInfo.Kind}): {sql}";
    }
}

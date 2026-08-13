using System.Data;
using System.Data.Common;
using Loader.Core.Abstractions;
using Loader.Core.Providers.Sql;
using Loader.Core.Sources;
using Ydb.Sdk.Ado;

namespace Loader.Core.Providers.Ydb;

/// <summary>
/// Provider потокового чтения YQL-запросов из YDB через native Ydb.Sdk ADO.NET.
/// </summary>
public sealed class YdbProvider : IProvider<IDatabaseSource, SqlTableConfig>
{
    public string Kind => "ydb";

    public async ValueTask<DbDataReader> OpenReaderAsync(
        IDatabaseSource source,
        SqlTableConfig config,
        CancellationToken cancellationToken = default)
    {
        var connection = new YdbConnection(source.ConnectionString);

        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var command = connection.CreateCommand();
            command.CommandText = config.Sql;

            return await command
                .ExecuteReaderAsync(CommandBehavior.CloseConnection, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw new DbExecutionException(Kind, config.Sql, ex);
        }
    }
}

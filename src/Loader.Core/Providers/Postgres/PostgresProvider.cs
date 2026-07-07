using System.Data;
using System.Data.Common;
using Loader.Core.Abstractions;
using Loader.Core.Providers;
using Loader.Core.Providers.Sql;
using Loader.Core.Sources;
using Npgsql;

namespace Loader.Core.Providers.Postgres;

/// <summary>
/// Provider потокового чтения SQL-запросов из Postgres.
/// </summary>
public sealed class PostgresProvider : IProvider<IDatabaseSource, SqlTableConfig>
{
    public string Kind => "postgres";

    public async ValueTask<DbDataReader> OpenReaderAsync(
        IDatabaseSource source,
        SqlTableConfig config,
        CancellationToken cancellationToken = default)
    {
        var connection = new NpgsqlConnection(source.ConnectionString);

        try
        {
            // 1. Открываем соединение из source.
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            // 2. Создаем команду из table config.
            var command = connection.CreateCommand();
            command.CommandText = config.Sql;

            // 3. Возвращаем потоковый reader; закрытие reader закроет соединение.
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

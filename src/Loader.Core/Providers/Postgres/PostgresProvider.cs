using System.Data;
using System.Data.Common;
using Loader.Core.Abstractions;
using Loader.Core.Decorators;
using Loader.Core.Providers;
using Loader.Core.Providers.Sql;
using Loader.Core.Sources;
using Npgsql;

namespace Loader.Core.Providers.Postgres;

/// <summary>
/// Provider for streaming SQL query results from Postgres.
/// </summary>
public sealed class PostgresProvider : IProvider<IDatabaseSource, SqlTableConfig>
{
    public string Kind => "postgres";

    public async ValueTask<DbDataReader> OpenReaderAsync(
        IDatabaseSource source,
        SqlTableConfig config,
        CancellationToken cancellationToken = default)
    {
        var dataSource = new NpgsqlDataSourceBuilder(source.ConnectionString)
            .EnableUnmappedTypes()
            .Build();
        NpgsqlConnection? connection = null;
        NpgsqlCommand? command = null;

        try
        {
            connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

            command = connection.CreateCommand();
            command.CommandText = config.Sql;

            var reader = await command
                .ExecuteReaderAsync(CommandBehavior.CloseConnection, cancellationToken)
                .ConfigureAwait(false);
            return new PostgresDataReader(reader, command, connection, dataSource);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (command is not null)
            {
                await command.DisposeAsync().ConfigureAwait(false);
            }

            if (connection is not null)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }

            await dataSource.DisposeAsync().ConfigureAwait(false);
            throw new DbExecutionException(Kind, config.Sql, ex);
        }
    }

    // The reader already owns the connection via CommandBehavior.CloseConnection, but it does not own
    // the per-open NpgsqlDataSource that carries EnableUnmappedTypes mappings. Keep all query resources
    // alive until the consumer disposes the returned reader, then release them in reader-first order.
    private sealed class PostgresDataReader : DbDataReaderDecorator
    {
        private readonly NpgsqlCommand command;
        private readonly NpgsqlConnection connection;
        private readonly NpgsqlDataSource dataSource;

        public PostgresDataReader(
            DbDataReader inner,
            NpgsqlCommand command,
            NpgsqlConnection connection,
            NpgsqlDataSource dataSource)
            : base(inner)
        {
            this.command = command;
            this.connection = connection;
            this.dataSource = dataSource;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                base.Dispose(disposing);
                command.Dispose();
                connection.Dispose();
                dataSource.Dispose();
                return;
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync().ConfigureAwait(false);
            await command.DisposeAsync().ConfigureAwait(false);
            await connection.DisposeAsync().ConfigureAwait(false);
            await dataSource.DisposeAsync().ConfigureAwait(false);
        }
    }
}

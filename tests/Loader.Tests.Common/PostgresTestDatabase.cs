using Npgsql;
using Testcontainers.PostgreSql;
using TUnit.Core.Interfaces;

namespace Loader.Tests.Common;

public sealed class PostgresTestDatabase : IAsyncInitializer, IAsyncDisposable
{
    private PostgreSqlContainer? container;

    public string ConnectionString =>
        container?.GetConnectionString() ?? throw new InvalidOperationException("Postgres test database is not started.");

    public static async Task<PostgresTestDatabase> StartAsync(CancellationToken cancellationToken = default)
    {
        var database = new PostgresTestDatabase();
        await database.StartCoreAsync(cancellationToken).ConfigureAwait(false);
        return database;
    }

    public Task InitializeAsync()
    {
        return StartCoreAsync(CancellationToken.None);
    }

    public async Task ExecuteAsync(string sql, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (container is not null)
        {
            await container.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task StartCoreAsync(CancellationToken cancellationToken)
    {
        container = new PostgreSqlBuilder()
            .WithImage(TestDatabaseImages.Postgres)
            .WithDatabase("loader_tests")
            .WithUsername("loader")
            .WithPassword("loader")
            .Build();

        await container.StartAsync(cancellationToken).ConfigureAwait(false);
    }
}

using Npgsql;
using Testcontainers.PostgreSql;

namespace Loader.Core.Tests.Infrastructure;

internal sealed class PostgresTestDatabase : IAsyncDisposable
{
    private readonly PostgreSqlContainer _container;

    private PostgresTestDatabase(PostgreSqlContainer container)
    {
        _container = container;
    }

    public string ConnectionString => _container.GetConnectionString();

    public static async Task<PostgresTestDatabase> StartAsync(CancellationToken cancellationToken = default)
    {
        var container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("loader_tests")
            .WithUsername("loader")
            .WithPassword("loader")
            .Build();

        await container.StartAsync(cancellationToken).ConfigureAwait(false);
        return new PostgresTestDatabase(container);
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
        await _container.DisposeAsync().ConfigureAwait(false);
    }
}

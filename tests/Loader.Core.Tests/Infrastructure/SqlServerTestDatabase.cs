using Testcontainers.MsSql;

namespace Loader.Core.Tests.Infrastructure;

internal sealed class SqlServerTestDatabase : IAsyncDisposable
{
    private readonly MsSqlContainer _container;

    private SqlServerTestDatabase(MsSqlContainer container)
    {
        _container = container;
    }

    public string ConnectionString => _container.GetConnectionString();

    public static async Task<SqlServerTestDatabase> StartAsync(CancellationToken cancellationToken = default)
    {
        var container = new MsSqlBuilder()
            .WithPassword("Loader_tests1!")
            .Build();

        await container.StartAsync(cancellationToken).ConfigureAwait(false);
        return new SqlServerTestDatabase(container);
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync().ConfigureAwait(false);
    }
}

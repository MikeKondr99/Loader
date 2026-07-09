using Testcontainers.ClickHouse;

namespace Loader.Core.Tests.Infrastructure;

internal sealed class ClickHouseTestDatabase : IAsyncDisposable
{
    private readonly ClickHouseContainer _container;

    private ClickHouseTestDatabase(ClickHouseContainer container)
    {
        _container = container;
    }

    public string ConnectionString => _container.GetConnectionString();

    public static async Task<ClickHouseTestDatabase> StartAsync(CancellationToken cancellationToken = default)
    {
        var container = new ClickHouseBuilder()
            .WithDatabase("loader_tests")
            .WithUsername("loader")
            .WithPassword("loader")
            .Build();

        await container.StartAsync(cancellationToken).ConfigureAwait(false);
        return new ClickHouseTestDatabase(container);
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync().ConfigureAwait(false);
    }
}

using Testcontainers.ClickHouse;
using TUnit.Core.Interfaces;

namespace Loader.Tests.Common;

public sealed class ClickHouseTestDatabase : IAsyncInitializer, IAsyncDisposable
{
    private ClickHouseContainer? container;

    public string ConnectionString =>
        container?.GetConnectionString() ?? throw new InvalidOperationException("ClickHouse test database is not started.");

    public static async Task<ClickHouseTestDatabase> StartAsync(CancellationToken cancellationToken = default)
    {
        var database = new ClickHouseTestDatabase();
        await database.StartCoreAsync(cancellationToken).ConfigureAwait(false);
        return database;
    }

    public async Task InitializeAsync()
    {
        await StartCoreAsync(CancellationToken.None).ConfigureAwait(false);
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
        container = new ClickHouseBuilder()
            .WithDatabase("loader_tests")
            .WithUsername("loader")
            .WithPassword("loader")
            .Build();

        await container.StartAsync(cancellationToken).ConfigureAwait(false);
    }
}

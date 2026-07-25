using Testcontainers.ClickHouse;
using TUnit.Core.Interfaces;

namespace Loader.Script.Tests.Infrastructure;

public sealed class ClickHouseTestDatabase : IAsyncInitializer, IAsyncDisposable
{
    private ClickHouseContainer? container;

    public string ConnectionString =>
        container?.GetConnectionString() ?? throw new InvalidOperationException("ClickHouse test database is not started.");

    public async Task InitializeAsync()
    {
        container = new ClickHouseBuilder()
            .WithDatabase("loader_script_tests")
            .WithUsername("loader")
            .WithPassword("loader")
            .Build();

        await container.StartAsync(CancellationToken.None).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (container is not null)
        {
            await container.DisposeAsync().ConfigureAwait(false);
        }
    }
}

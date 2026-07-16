using Testcontainers.ClickHouse;
using TUnit.Core.Interfaces;

namespace Loader.Query.Tests.Infrastructure;

public sealed class ClickHouseTestDatabase : IAsyncInitializer, IAsyncDisposable
{
    private ClickHouseContainer? container;

    public string ConnectionString =>
        container?.GetConnectionString() ?? throw new InvalidOperationException("ClickHouse test database is not started.");

    public async Task InitializeAsync()
    {
        // 1. Создаем один контейнер, который TUnit переиспользует как assembly resource.
        container = new ClickHouseBuilder()
            .WithDatabase("loader_query_tests")
            .WithUsername("loader")
            .WithPassword("loader")
            .Build();

        // 2. Поднимаем ClickHouse до запуска тестов, которые получили этот resource.
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

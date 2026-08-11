using Testcontainers.Oracle;
using TUnit.Core.Interfaces;

namespace Loader.Tests.Common;

public sealed class OracleTestDatabase : IAsyncInitializer, IAsyncDisposable
{
    private OracleContainer? container;

    public string ConnectionString =>
        container?.GetConnectionString() ?? throw new InvalidOperationException("Oracle test database is not started.");

    public static async Task<OracleTestDatabase> StartAsync(CancellationToken cancellationToken = default)
    {
        var database = new OracleTestDatabase();
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
        container = new OracleBuilder()
            .WithImage(TestDatabaseImages.Oracle)
            .WithUsername("loader")
            .WithPassword("Loader_tests1!")
            .Build();

        await container.StartAsync(cancellationToken).ConfigureAwait(false);
    }
}

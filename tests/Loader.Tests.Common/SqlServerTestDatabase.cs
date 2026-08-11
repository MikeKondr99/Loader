using Testcontainers.MsSql;
using TUnit.Core.Interfaces;

namespace Loader.Tests.Common;

public sealed class SqlServerTestDatabase : IAsyncInitializer, IAsyncDisposable
{
    private MsSqlContainer? container;

    public string ConnectionString =>
        container?.GetConnectionString() ?? throw new InvalidOperationException("SqlServer test database is not started.");

    public static async Task<SqlServerTestDatabase> StartAsync(CancellationToken cancellationToken = default)
    {
        var database = new SqlServerTestDatabase();
        await database.StartCoreAsync(cancellationToken).ConfigureAwait(false);
        return database;
    }

    public Task InitializeAsync()
    {
        return StartCoreAsync(CancellationToken.None);
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
        container = new MsSqlBuilder()
            .WithImage(TestDatabaseImages.SqlServer)
            .WithPassword("Loader_tests1!")
            .Build();

        await container.StartAsync(cancellationToken).ConfigureAwait(false);
    }
}

using Testcontainers.MariaDb;
using TUnit.Core.Interfaces;
using System.Data.Odbc;

namespace Loader.Tests.Common;

public sealed class OdbcMariaDbTestDatabase : IAsyncInitializer, IAsyncDisposable
{
    private const string DatabaseName = "loader_tests";
    private const string Username = "loader";
    private const string Password = "loader";

    private MariaDbContainer? container;
    private OdbcConnection? connection;

    public string ConnectionString =>
        container is null
            ? throw new InvalidOperationException("ODBC MariaDB test database is not started.")
            : $"Driver={{MariaDB ODBC 3.2 Driver}};Server={container.Hostname};Port={container.GetMappedPublicPort(MariaDbBuilder.MariaDbPort)};Database={DatabaseName};User={Username};Password={Password};Option=3";

    public OdbcConnection Connection =>
        connection ?? throw new InvalidOperationException("ODBC MariaDB connection is not opened.");

    public static async Task<OdbcMariaDbTestDatabase> StartAsync(CancellationToken cancellationToken = default)
    {
        var database = new OdbcMariaDbTestDatabase();
        await database.StartCoreAsync(cancellationToken).ConfigureAwait(false);
        return database;
    }

    public Task InitializeAsync()
    {
        return StartCoreAsync(CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        if (connection is not null)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }

        if (container is not null)
        {
            await container.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task StartCoreAsync(CancellationToken cancellationToken)
    {
        container = new MariaDbBuilder()
            .WithImage(TestDatabaseImages.MariaDb)
            .WithDatabase(DatabaseName)
            .WithUsername(Username)
            .WithPassword(Password)
            .Build();

        await container.StartAsync(cancellationToken).ConfigureAwait(false);
        connection = new OdbcConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    }
}

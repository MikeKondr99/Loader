using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using TUnit.Core.Interfaces;
using Ydb.Sdk.Ado;

namespace Loader.Tests.Common;

public sealed class YdbTestDatabase : IAsyncInitializer, IAsyncDisposable
{
    private const int GrpcPort = 2136;
    private const int GrpcsPort = 2135;
    private const int MonitorPort = 8765;

    private IContainer? container;
    private int grpcPort = GrpcPort;

    public string ConnectionString
    {
        get
        {
            if (container is null)
            {
                throw new InvalidOperationException("YDB test database is not started.");
            }

            return new YdbConnectionStringBuilder
            {
                Host = "localhost",
                Port = grpcPort,
                Database = "/local"
            }.ConnectionString;
        }
    }

    public static async Task<YdbTestDatabase> StartAsync(CancellationToken cancellationToken = default)
    {
        var database = new YdbTestDatabase();
        await database.StartCoreAsync(cancellationToken).ConfigureAwait(false);
        return database;
    }

    public Task InitializeAsync()
    {
        return StartCoreAsync(CancellationToken.None);
    }

    public async Task ExecuteAsync(string sql, CancellationToken cancellationToken = default)
    {
        await using var connection = new YdbConnection(ConnectionString);
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
        var grpcsPort = GetFreeTcpPort();
        grpcPort = GetFreeTcpPort();
        var monitorPort = GetFreeTcpPort();

        container = new ContainerBuilder()
            .WithImage(TestDatabaseImages.Ydb)
            .WithHostname("localhost")
            .WithPortBinding(grpcsPort, grpcsPort)
            .WithPortBinding(grpcPort, grpcPort)
            .WithPortBinding(monitorPort, monitorPort)
            .WithEnvironment("GRPC_TLS_PORT", grpcsPort.ToString(CultureInfo.InvariantCulture))
            .WithEnvironment("GRPC_PORT", grpcPort.ToString(CultureInfo.InvariantCulture))
            .WithEnvironment("MON_PORT", monitorPort.ToString(CultureInfo.InvariantCulture))
            .WithEnvironment("YDB_ANONYMOUS_CREDENTIALS", "true")
            .WithEnvironment("YDB_USE_IN_MEMORY_PDISKS", "true")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilContainerIsHealthy())
            .Build();

        await container.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, port: 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

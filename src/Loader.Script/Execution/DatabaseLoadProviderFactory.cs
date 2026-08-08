using System.Data.Common;
using Loader.Core.Providers.ClickHouse;
using Loader.Core.Providers.Hive;
using Loader.Core.Providers.Oracle;
using Loader.Core.Providers.Postgres;
using Loader.Core.Providers.Sql;
using Loader.Core.Providers.SqlServer;
using Loader.Core.Sources;

namespace Loader.Script.Execution;

internal sealed class DatabaseLoadProviderFactory
{
    private static readonly IReadOnlyDictionary<ScriptConnectionType, DatabaseLoadProviderFactory> KnownFactories = CreateKnownFactories();

    private readonly Func<IDatabaseSource, SqlTableConfig, CancellationToken, ValueTask<DbDataReader>> open;

    private DatabaseLoadProviderFactory(
        ScriptConnectionType provider,
        string kind,
        bool requiresBuffer,
        Func<IDatabaseSource, SqlTableConfig, CancellationToken, ValueTask<DbDataReader>> open)
    {
        Provider = provider;
        Kind = kind;
        RequiresBuffer = requiresBuffer;
        this.open = open;
    }

    public ScriptConnectionType Provider { get; }

    public string Kind { get; }

    public bool RequiresBuffer { get; }

    public static bool TryGet(ScriptConnectionType provider, out DatabaseLoadProviderFactory factory)
    {
        return KnownFactories.TryGetValue(provider, out factory!);
    }

    public LoadProviderSource CreateSource(string connectionString, string sql)
    {
        var source = new ConnectionStringSource { ConnectionString = connectionString };
        var config = new SqlTableConfig { Sql = sql };
        return new LoadProviderSource
        {
            Kind = Kind,
            RequiresBuffer = RequiresBuffer,
            OpenReaderAsync = token => open(source, config, token)
        };
    }

    private static IReadOnlyDictionary<ScriptConnectionType, DatabaseLoadProviderFactory> CreateKnownFactories()
    {
        DatabaseLoadProviderFactory[] factories =
        [
            new(
                ScriptConnectionType.Postgres,
                "postgres",
                requiresBuffer: false,
                static (source, config, token) => new PostgresProvider().OpenReaderAsync(source, config, token)),
            new(
                ScriptConnectionType.SqlServer,
                "sqlserver",
                requiresBuffer: true,
                static (source, config, token) => new SqlServerProvider().OpenReaderAsync(source, config, token)),
            new(
                ScriptConnectionType.Oracle,
                "oracle",
                requiresBuffer: true,
                static (source, config, token) => new OracleProvider().OpenReaderAsync(source, config, token)),
            new(
                ScriptConnectionType.Hive,
                "hive",
                requiresBuffer: true,
                static (source, config, token) => new HiveProvider().OpenReaderAsync(source, config, token)),
            new(
                ScriptConnectionType.ClickHouse,
                "clickhouse",
                requiresBuffer: false,
                static (source, config, token) => new ClickHouseProvider().OpenReaderAsync(source, config, token))
        ];

        return factories.ToDictionary(
            static factory => factory.Provider,
            static factory => factory,
            EqualityComparer<ScriptConnectionType>.Default);
    }
}

using System.Data.Common;
using System.Globalization;
using ClickHouse.Client.ADO;
using ClickHouse.Client.Numerics;
using Loader.Core.Decorators;
using Loader.Core.Providers.ClickHouse;
using Loader.Core.Providers.Sql;
using Loader.Core.Sources;
using Loader.Core.Writers.ClickHouse;
using Loader.Lang;
using Loader.Script.Execution;
using TUnit.Assertions.Enums;
using LangScript = Loader.Lang.Script;

namespace Loader.Script.Tests.Infrastructure;

internal static class ScriptIntegrationAssert
{
    public const string ContainerClickHouse = "container_ch";
    public const string ContainerPostgres = "container_pg";
    public const string ContainerSqlServer = "container_mssql";
    public const string ContainerOracle = "container_oracle";
    public const string ContainerHive = "container_hive";

    private static readonly ScriptExecutor Executor = new();

    public static ScriptContext CreateContext(ClickHouseTestDatabase database)
    {
        return new ScriptContext
        {
            FileStorage = new FileSystemSource(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Script")),
            TargetConnectionString = database.ConnectionString,
            ConnectionRegistry = ContainerConnections(clickHouse: database)
        };
    }

    public static IConnectionRegistry ContainerConnections(
        ClickHouseTestDatabase? clickHouse = null,
        PostgresTestDatabase? postgres = null,
        SqlServerTestDatabase? sqlServer = null,
        OracleTestDatabase? oracle = null,
        string? hiveConnectionString = null)
    {
        var connections = new List<ScriptConnection>();
        if (clickHouse is not null)
        {
            connections.Add(Connection(ContainerClickHouse, ScriptConnectionType.ClickHouse, clickHouse.ConnectionString));
        }

        if (postgres is not null)
        {
            connections.Add(Connection(ContainerPostgres, ScriptConnectionType.Postgres, postgres.ConnectionString));
        }

        if (sqlServer is not null)
        {
            connections.Add(Connection(ContainerSqlServer, ScriptConnectionType.SqlServer, sqlServer.ConnectionString));
        }

        if (oracle is not null)
        {
            connections.Add(Connection(ContainerOracle, ScriptConnectionType.Oracle, oracle.ConnectionString));
        }

        if (!string.IsNullOrWhiteSpace(hiveConnectionString))
        {
            connections.Add(Connection(ContainerHive, ScriptConnectionType.Hive, hiveConnectionString));
        }

        return connections.Count == 0
            ? EmptyConnectionRegistry.Instance
            : new InMemoryConnectionRegistry(connections);
    }

    public static async Task<ScriptExecutionResult> ExecuteScriptAsync(
        ClickHouseTestDatabase database,
        string scriptText,
        IConnectionRegistry? connectionRegistry = null,
        CancellationToken cancellationToken = default)
    {
        var executionId = Guid.NewGuid().ToString("N");
        var tempPrefix = $"script_test_temp_{executionId}_";
        var finalPrefix = $"script_test_final_{executionId}_";
        var context = CreateContext(database) with
        {
            ConnectionRegistry = connectionRegistry ?? ContainerConnections(clickHouse: database),
            Options = new ScriptContextOptions
            {
                TempTablePrefix = tempPrefix,
                FinalTablePrefix = finalPrefix
            }
        };

        var tables = await Executor.ExecuteAsync(context, ParseScript(scriptText), cancellationToken)
            .ConfigureAwait(false);
        return new ScriptExecutionResult(tables, tempPrefix, finalPrefix);
    }

    public static Task<ScriptExecutionResult> ExecuteScriptAsync(
        ClickHouseTestDatabase database,
        string scriptText,
        PostgresTestDatabase postgres,
        CancellationToken cancellationToken = default)
    {
        return ExecuteScriptAsync(
            database,
            scriptText,
            ContainerConnections(clickHouse: database, postgres: postgres),
            cancellationToken);
    }

    public static Task<ScriptExecutionResult> ExecuteScriptAsync(
        ClickHouseTestDatabase database,
        string scriptText,
        SqlServerTestDatabase sqlServer,
        CancellationToken cancellationToken = default)
    {
        return ExecuteScriptAsync(
            database,
            scriptText,
            ContainerConnections(clickHouse: database, sqlServer: sqlServer),
            cancellationToken);
    }

    public static Task<ScriptExecutionResult> ExecuteScriptAsync(
        ClickHouseTestDatabase database,
        string scriptText,
        OracleTestDatabase oracle,
        CancellationToken cancellationToken = default)
    {
        return ExecuteScriptAsync(
            database,
            scriptText,
            ContainerConnections(clickHouse: database, oracle: oracle),
            cancellationToken);
    }

    public static Task<ScriptExecutionResult> ExecuteScriptWithOnlyClickHouseAsync(
        ClickHouseTestDatabase database,
        string scriptText,
        CancellationToken cancellationToken = default)
    {
        return ExecuteScriptAsync(
            database,
            scriptText,
            ContainerConnections(clickHouse: database),
            cancellationToken);
    }

    private static ScriptConnection Connection(
        string name,
        ScriptConnectionType provider,
        string connectionString)
    {
        return new ScriptConnection
        {
            Name = name,
            Provider = provider,
            ConnectionString = connectionString
        };
    }

    public static async Task AssertFinalTableAsync(
        ClickHouseTestDatabase database,
        LoadedTable table,
        IReadOnlyList<string> expectedColumns,
        IReadOnlyList<IReadOnlyList<object?>> expectedRows,
        string? orderBySql = null)
    {
        var rows = await ReadRowsAsync(database, table.Name, orderBySql).ConfigureAwait(false);

        await Assert.That(table.Fields.Select(static field => field.Name).ToArray())
            .IsEquivalentTo(expectedColumns, CollectionOrdering.Matching);
        await Assert.That(rows.Columns).IsEquivalentTo(AbstractColumnNames(expectedColumns.Count), CollectionOrdering.Matching);
        await Assert.That(rows.Rows).IsEquivalentTo(expectedRows, CollectionOrdering.Matching);
    }

    public static Task<QueryRows> ReadFinalTableAsync(
        ClickHouseTestDatabase database,
        LoadedTable table,
        string? orderBySql = null)
    {
        return ReadRowsAsync(database, table.Name, orderBySql);
    }

    public static async Task AssertNoTablesWithPrefixAsync(
        ClickHouseTestDatabase database,
        string prefix)
    {
        var count = await CountTablesWithPrefixAsync(database, prefix).ConfigureAwait(false);

        await Assert.That(Convert.ToInt64(count, CultureInfo.InvariantCulture)).IsEqualTo(0);
    }

    public static async Task AssertTableCountWithPrefixAsync(
        ClickHouseTestDatabase database,
        string prefix,
        long expectedCount)
    {
        var count = await CountTablesWithPrefixAsync(database, prefix).ConfigureAwait(false);

        await Assert.That(Convert.ToInt64(count, CultureInfo.InvariantCulture)).IsEqualTo(expectedCount);
    }

    public static Task AssertNoTempTablesAsync(
        ClickHouseTestDatabase database,
        ScriptExecutionResult result)
    {
        return AssertNoTablesWithPrefixAsync(database, result.TempTablePrefix);
    }

    public static async Task ExecuteClickHouseAsync(
        ClickHouseTestDatabase database,
        string sql,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new ClickHouseConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<QueryRows> ReadRowsAsync(
        ClickHouseTestDatabase database,
        ClickHouseTableName tableName,
        string? orderBySql)
    {
        await using var rawReader = await new ClickHouseProvider()
            .OpenReaderAsync(
                new ConnectionStringSource
                {
                    ConnectionString = database.ConnectionString
                },
                new SqlTableConfig
                {
                    Sql = $"SELECT * FROM {tableName.ToSql()} {orderBySql}"
                })
            .ConfigureAwait(false);
        await using var reader = rawReader.Normalize();

        var columns = Enumerable.Range(0, reader.FieldCount)
            .Select(reader.GetName)
            .ToArray();
        var rows = await ReadRowsAsync(reader).ConfigureAwait(false);
        return new QueryRows(columns, rows);
    }

    private static async Task<object?> ExecuteScalarAsync(
        ClickHouseTestDatabase database,
        string sql)
    {
        await using var connection = new ClickHouseConnection(database.ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync().ConfigureAwait(false);
    }

    private static Task<object?> CountTablesWithPrefixAsync(
        ClickHouseTestDatabase database,
        string prefix)
    {
        return ExecuteScalarAsync(
            database,
            $"SELECT count() FROM system.tables WHERE database = currentDatabase() AND startsWith(name, '{EscapeSqlString(prefix)}')");
    }

    private static LangScript ParseScript(string text)
    {
        var result = LangScript.Parse(text);
        if (result.IsSuccess)
        {
            return result.Value;
        }

        throw new InvalidOperationException(result.Error.Message);
    }

    private static async Task<object?[][]> ReadRowsAsync(DbDataReader reader)
    {
        var rows = new List<object?[]>();
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            var row = new object?[reader.FieldCount];
            for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++)
            {
                row[ordinal] = reader.IsDBNull(ordinal) ? null : NormalizeAssertValue(reader.GetValue(ordinal));
            }

            rows.Add(row);
        }

        return rows.ToArray();
    }

    private static string EscapeSqlString(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }

    private static object NormalizeAssertValue(object value)
    {
        return value is ClickHouseDecimal decimalValue
            ? decimalValue.ToDecimal(CultureInfo.InvariantCulture)
            : value;
    }

    private static string[] AbstractColumnNames(int count)
    {
        return Enumerable.Range(1, count)
            .Select(static ordinal => $"column{ordinal}")
            .ToArray();
    }

    public sealed record QueryRows(string[] Columns, object?[][] Rows);
}

internal sealed record ScriptExecutionResult(
    IReadOnlyList<LoadedTable> Tables,
    string TempTablePrefix,
    string FinalTablePrefix);

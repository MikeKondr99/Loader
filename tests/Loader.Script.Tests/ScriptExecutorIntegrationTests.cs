using System.Data.Common;
using ClickHouse.Client.ADO;
using Loader.Core.Decorators;
using Loader.Core.Providers.ClickHouse;
using Loader.Core.Providers.Sql;
using Loader.Core.Sources;
using Loader.Core.Writers.ClickHouse;
using Loader.Lang;
using Loader.Script.Execution;
using Loader.Tests.Common;
using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Assertions.Enums;
using LangScript = Loader.Lang.Script;

namespace Loader.Script.Tests;

[ClassDataSource<ClickHouseTestDatabase>(Shared = SharedType.PerTestSession)]
[ParallelLimiter<ClickHouseParallelLimit>]
public sealed class ScriptExecutorIntegrationTests
{
    private readonly ClickHouseTestDatabase database;

    public ScriptExecutorIntegrationTests(ClickHouseTestDatabase database)
    {
        this.database = database;
    }

    [Test]
    [DisplayName("ScriptExecutor выполняет несколько LOAD из CSV и ClickHouse в final таблицы DWH")]
    public async Task Execute_script_loads_multiple_sources_into_clickhouse()
    {
        // Arrange
        var sourceTable = $"source_users_{Guid.NewGuid():N}";
        await CreateClickHouseSourceTableAsync(sourceTable);
        var fileStorage = new FileSystemSource(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Script"));
        var context = new ScriptContext
        {
            FileStorage = fileStorage,
            TargetConnectionString = database.ConnectionString,
            Logger = NullLogger.Instance
        };
        var script = ParseScript(
            $$"""
            csv_names:
            LOAD
                name,
                Upper(name) AS upper_name
            FROM [orders.csv] (csv)
            WHERE name != 'Bob'
            ORDER BY name DESC;

            ch_users:
            LOAD
                username,
                city
            FROM [{{database.ConnectionString}}] (clickhouse, table='{{sourceTable}}')
            WHERE city != 'Berlin'
            ORDER BY username ASC;
            """);
        var executor = new ScriptExecutor
        {
            LoadStatementExecutor = new LoadStatementExecutor
            {
                TempTablePrefix = "script_it_temp_",
                FinalTablePrefix = "script_it_final_"
            }
        };

        // Act
        var result = await executor.ExecuteAsync(context, script);

        // Assert
        await Assert.That(result).Count().IsEqualTo(2);
        await Assert.That(result[0].Alias).IsEqualTo("csv_names");
        await Assert.That(result[1].Alias).IsEqualTo("ch_users");
        await Assert.That(result.Select(static table => table.Name.Table).ToArray())
            .All().Satisfy(static name => name.StartsWith("script_it_final_", StringComparison.Ordinal));

        await AssertFinalTableAsync(
            result[0].Name,
            orderBySql: "ORDER BY `name` DESC",
            expectedColumns: ["name", "upper_name"],
            expectedRows:
            [
                ["Charlie", "CHARLIE"],
                ["Alice", "ALICE"]
            ]);

        await AssertFinalTableAsync(
            result[1].Name,
            orderBySql: "ORDER BY `username` ASC",
            expectedColumns: ["username", "city"],
            expectedRows:
            [
                ["anna", "London"],
                ["mike", "Moscow"]
            ]);
    }

    private async Task CreateClickHouseSourceTableAsync(string tableName)
    {
        await ExecuteClickHouseAsync(
            $"""
            CREATE TABLE `{tableName}`
            (
                `username` String,
                `city` String
            )
            ENGINE = Memory
            """);
        await ExecuteClickHouseAsync(
            $"""
            INSERT INTO `{tableName}` (`username`, `city`) VALUES
            ('mike', 'Moscow'),
            ('anna', 'London'),
            ('bob', 'Berlin')
            """);
    }

    private async Task AssertFinalTableAsync(
        ClickHouseTableName tableName,
        string orderBySql,
        string[] expectedColumns,
        string[][] expectedRows)
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
                });
        await using var reader = rawReader.Normalize();

        await Assert.That(Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray())
            .IsEquivalentTo(expectedColumns, CollectionOrdering.Matching);

        var rows = await ReadRowsAsync(reader);
        await Assert.That(rows).IsEquivalentTo(expectedRows, CollectionOrdering.Matching);
    }

    private async Task ExecuteClickHouseAsync(string sql)
    {
        await using var connection = new ClickHouseConnection(database.ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
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

    private static async Task<string[][]> ReadRowsAsync(DbDataReader reader)
    {
        var rows = new List<string[]>();
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            var row = new string[reader.FieldCount];
            for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++)
            {
                row[ordinal] = reader.GetString(ordinal);
            }

            rows.Add(row);
        }

        return rows.ToArray();
    }
}

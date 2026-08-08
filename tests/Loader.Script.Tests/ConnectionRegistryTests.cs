namespace Loader.Script.Tests;

public sealed class ConnectionRegistryTests
{
    [Test]
    [DisplayName("AggregateConnectionRegistry возвращает connection из первого registry где найдено имя")]
    public async Task Aggregate_returns_first_matching_connection()
    {
        // Arrange
        var first = new InMemoryConnectionRegistry(
        [
            new ScriptConnection
            {
                Name = "main",
                Provider = ScriptConnectionType.Postgres,
                ConnectionString = "Host=first"
            }
        ]);
        var second = new InMemoryConnectionRegistry(
        [
            new ScriptConnection
            {
                Name = "main",
                Provider = ScriptConnectionType.ClickHouse,
                ConnectionString = "Host=second"
            }
        ]);
        var registry = new AggregateConnectionRegistry([first, second]);

        // Act
        var connection = await registry.GetAsync("main");

        // Assert
        await Assert.That(connection).IsNotNull();
        await Assert.That(connection!.Provider).IsEqualTo(ScriptConnectionType.Postgres);
        await Assert.That(connection.ConnectionString).IsEqualTo("Host=first");
    }

    [Test]
    [DisplayName("AggregateConnectionRegistry объединяет имена подключений без дублей")]
    public async Task Aggregate_merges_connection_names()
    {
        // Arrange
        var first = new InMemoryConnectionRegistry(
        [
            Connection("pg", ScriptConnectionType.Postgres),
            Connection("shared", ScriptConnectionType.Postgres)
        ]);
        var second = new InMemoryConnectionRegistry(
        [
            Connection("ch", ScriptConnectionType.ClickHouse),
            Connection("shared", ScriptConnectionType.ClickHouse)
        ]);
        var registry = new AggregateConnectionRegistry([first, second]);

        // Act
        var names = await registry.FindNamesAsync();

        // Assert
        await Assert.That(names).IsEquivalentTo(["ch", "pg", "shared"]);
    }

    private static ScriptConnection Connection(string name, ScriptConnectionType type)
    {
        return new ScriptConnection
        {
            Name = name,
            Provider = type,
            ConnectionString = $"Host={name}"
        };
    }
}

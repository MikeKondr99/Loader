using Loader.Core.Providers.Jdbc;

namespace Loader.Core.Tests;

public sealed class JdbcProviderSmokeTests
{
    [Test]
    [DisplayName("JDBC provider загружает Hive 4.0.0 driver class из jar")]
    public async Task Jdbc_provider_loads_hive_4_driver_class()
    {
        await AssertDriverAsync(
            "LOADER_TEST_HIVE_4_JDBC_JAR",
            "org.apache.hive.jdbc.HiveDriver",
            "jdbc:hive2://localhost:10000/default");
    }

    [Test]
    [DisplayName("JDBC provider загружает Kyuubi 1.9 driver class из jar")]
    public async Task Jdbc_provider_loads_kyuubi_1_9_driver_class()
    {
        await AssertDriverAsync(
            "LOADER_TEST_KYUUBI_1_9_JDBC_JAR",
            "org.apache.kyuubi.jdbc.KyuubiHiveDriver",
            "jdbc:kyuubi://localhost:10009/default");
    }

    private static async Task AssertDriverAsync(string envName, string driverClass, string jdbcUrl)
    {
        var jarPath = Environment.GetEnvironmentVariable(envName);
        if (string.IsNullOrWhiteSpace(jarPath))
        {
            return;
        }

        var loader = JdbcDriverLoader.Create([jarPath]);
        var driver = JdbcDriverLoader.CreateDriver(loader, driverClass);

        await Assert.That(driver).IsNotNull();
        await Assert.That(driver.acceptsURL(jdbcUrl)).IsTrue();
    }
}

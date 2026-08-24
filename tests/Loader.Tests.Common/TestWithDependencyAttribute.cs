using TUnit.Core;
using TUnit.Core.Interfaces;

using System.Diagnostics;

namespace Loader.Tests.Common;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public sealed class TestWithDependencyAttribute :
    AsyncUntypedDataSourceGeneratorAttribute,
    ITestDiscoveryEventReceiver,
    ITestRegisteredEventReceiver
{
    public const bool SkipOracle = true;

    private readonly DatabaseDependency[] dependencies;

    public TestWithDependencyAttribute(params DatabaseDependency[] dependencies)
    {
        if (dependencies.Length == 0)
        {
            throw new ArgumentException("At least one test dependency must be specified.", nameof(dependencies));
        }

        this.dependencies = dependencies;
        SkipIfEmpty = true;
    }

    public int Order => 0;

    public string[]? Categories { get; set; }

    public bool UseDataSource { get; set; } = true;

    public bool CheckExternalDependencies { get; set; } = true;

    protected override async IAsyncEnumerable<Func<Task<object?[]?>>> GenerateDataSourcesAsync(
        DataGeneratorMetadata dataGeneratorMetadata)
    {
        await foreach (var row in GenerateDataSourceAsync(dataGeneratorMetadata).ConfigureAwait(false))
        {
            yield return row;
        }
    }

    public ValueTask OnTestDiscovered(DiscoveredTestContext context)
    {
        foreach (var category in Categories ?? DefaultCategories())
        {
            context.AddCategory(category);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask OnTestRegistered(TestRegisteredContext context)
    {
        context.SetParallelLimiter(CreateLimiter());

        var skipReason = GetSkipReason();
        if (skipReason is not null)
        {
            context.SetSkipped(skipReason);
        }

        return ValueTask.CompletedTask;
    }

    private async IAsyncEnumerable<Func<Task<object?[]?>>> GenerateDataSourceAsync(
        DataGeneratorMetadata dataGeneratorMetadata)
    {
        if (ShouldSkipOracle())
        {
            yield break;
        }

        if (!UseDataSource)
        {
            yield return () => Task.FromResult<object?[]?>([]);
            yield break;
        }

        var factories = new List<Func<Task<object?>>>(dependencies.Length);
        foreach (var dependency in dependencies)
        {
            if (dependency == DatabaseDependency.ApacheHive)
            {
                continue;
            }

            factories.Add(await CreateDependencyFactoryAsync(dependency, dataGeneratorMetadata).ConfigureAwait(false));
        }

        yield return async () =>
        {
            var values = new object?[factories.Count];
            for (var index = 0; index < factories.Count; index++)
            {
                values[index] = await factories[index]().ConfigureAwait(false);
            }

            return values;
        };
    }

    private static async Task<Func<Task<object?>>> CreateDependencyFactoryAsync(
        DatabaseDependency dependency,
        DataGeneratorMetadata dataGeneratorMetadata)
    {
        return dependency switch
        {
            DatabaseDependency.ClickHouse or DatabaseDependency.ClickHouseDwh =>
                await CreateDependencyFactoryAsync<ClickHouseTestDatabase>(dataGeneratorMetadata).ConfigureAwait(false),
            DatabaseDependency.Postgres =>
                await CreateDependencyFactoryAsync<PostgresTestDatabase>(dataGeneratorMetadata).ConfigureAwait(false),
            DatabaseDependency.OdbcMariaDb =>
                await CreateDependencyFactoryAsync<OdbcMariaDbTestDatabase>(dataGeneratorMetadata).ConfigureAwait(false),
            DatabaseDependency.SqlServer =>
                await CreateDependencyFactoryAsync<SqlServerTestDatabase>(dataGeneratorMetadata).ConfigureAwait(false),
            DatabaseDependency.Oracle =>
                await CreateDependencyFactoryAsync<OracleTestDatabase>(dataGeneratorMetadata).ConfigureAwait(false),
            DatabaseDependency.ApacheHive => throw new ArgumentOutOfRangeException(
                nameof(dependency),
                dependency,
                "ApacheHive dependency does not have a managed test datasource."),
            _ => throw new ArgumentOutOfRangeException(nameof(dependency), dependency, null)
        };
    }

    private static async Task<Func<Task<object?>>> CreateDependencyFactoryAsync<TDatabase>(
        DataGeneratorMetadata dataGeneratorMetadata)
    {
        var dataSource = new ClassDataSourceAttribute<TDatabase>
        {
            Shared = SharedType.PerTestSession
        };

        await foreach (var row in dataSource.GetDataRowsAsync(dataGeneratorMetadata).ConfigureAwait(false))
        {
            return async () =>
            {
                var values = await row().ConfigureAwait(false);
                return values is { Length: > 0 } ? values[0] : null;
            };
        }

        throw new InvalidOperationException($"No datasource row was generated for {typeof(TDatabase).Name}.");
    }

    private IEnumerable<string> DefaultCategories()
    {
        return dependencies
            .Select(Category)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal);
    }

    private static string? Category(DatabaseDependency dependency)
    {
        return dependency switch
        {
            DatabaseDependency.ClickHouse => TestCategories.ClickHouse,
            DatabaseDependency.ClickHouseDwh => null,
            DatabaseDependency.Postgres => TestCategories.Postgres,
            DatabaseDependency.OdbcMariaDb => TestCategories.OdbcMariaDb,
            DatabaseDependency.SqlServer => TestCategories.SqlServer,
            DatabaseDependency.Oracle => TestCategories.Oracle,
            DatabaseDependency.ApacheHive => TestCategories.ApacheHive,
            _ => throw new ArgumentOutOfRangeException(nameof(dependency), dependency, null)
        };
    }

    private IParallelLimit CreateLimiter()
    {
        return dependencies
            .Select(Limiter)
            .OrderBy(static limiter => limiter.Limit)
            .First();
    }

    private static IParallelLimit Limiter(DatabaseDependency dependency)
    {
        return dependency switch
        {
            DatabaseDependency.ClickHouse or DatabaseDependency.ClickHouseDwh => new ClickHouseParallelLimit(),
            DatabaseDependency.Postgres => new PostgresParallelLimit(),
            DatabaseDependency.OdbcMariaDb => new OdbcMariaDbParallelLimit(),
            DatabaseDependency.SqlServer => new SqlServerParallelLimit(),
            DatabaseDependency.Oracle => new OracleParallelLimit(),
            DatabaseDependency.ApacheHive => new ApacheHiveParallelLimit(),
            _ => throw new ArgumentOutOfRangeException(nameof(dependency), dependency, null)
        };
    }

    private string? GetSkipReason()
    {
        if (ShouldSkipOracle())
        {
            return "Oracle tests are temporarily skipped.";
        }

        if (!CheckExternalDependencies ||
            TestEnvironment.IsCi)
        {
            return null;
        }

        if (dependencies.Contains(DatabaseDependency.ApacheHive) && !OdbcDriver.ApacheHiveInstalled)
        {
            return "Apache Hive ODBC driver is not installed.";
        }

        if (dependencies.Contains(DatabaseDependency.OdbcMariaDb) && !OdbcDriver.OdbcMariaDbInstalled)
        {
            return "MariaDB ODBC driver is not installed.";
        }

        return null;
    }

    private bool ShouldSkipOracle()
    {
        return SkipOracle && dependencies.Contains(DatabaseDependency.Oracle);
    }

    private static class OdbcDriver
    {
        private static readonly Lazy<bool> ApacheHive = new(() => Detect("LOADER_TEST_APACHE_HIVE_ODBC", "Hive"));
        private static readonly Lazy<bool> OdbcMariaDb = new(() => Detect("LOADER_TEST_MARIADB_ODBC", "MariaDB"));

        public static bool ApacheHiveInstalled => ApacheHive.Value;

        public static bool OdbcMariaDbInstalled => OdbcMariaDb.Value;

        private static bool Detect(string overrideEnvironmentVariable, string driverNamePart)
        {
            if (Environment.GetEnvironmentVariable(overrideEnvironmentVariable) == "1")
            {
                return true;
            }

            if (OperatingSystem.IsWindows())
            {
                return HasWindowsOdbcDriver(Microsoft.Win32.Registry.LocalMachine) ||
                       HasWindowsOdbcDriver(Microsoft.Win32.Registry.CurrentUser);
            }

            return HasUnixOdbcDriver(driverNamePart);

            [System.Runtime.Versioning.SupportedOSPlatform("windows")]
            bool HasWindowsOdbcDriver(Microsoft.Win32.RegistryKey root)
            {
                try
                {
                    using var key = root.OpenSubKey(@"SOFTWARE\ODBC\ODBCINST.INI\ODBC Drivers");
                    return key?.GetValueNames()
                        .Any(name => name.Contains(driverNamePart, StringComparison.OrdinalIgnoreCase)) == true;
                }
                catch
                {
                    return false;
                }
            }

            static bool HasUnixOdbcDriver(string namePart)
            {
                // TODO: сделать выбор odbc драйвера для unix
                return false;
            }
        }
    }
}

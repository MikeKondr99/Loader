using TUnit.Core;
using TUnit.Core.Interfaces;

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
        await foreach (var row in GeneratePrimaryDataSourceAsync(dataGeneratorMetadata).ConfigureAwait(false))
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

    private IAsyncEnumerable<Func<Task<object?[]?>>> GeneratePrimaryDataSourceAsync(
        DataGeneratorMetadata dataGeneratorMetadata)
    {
        if (ShouldSkipOracle())
        {
            return GenerateNoDataSourceAsync();
        }

        if (!UseDataSource)
        {
            return GenerateEmptyDataSourceAsync();
        }

        return dependencies[0] switch
        {
            DatabaseDependency.ClickHouse or DatabaseDependency.ClickHouseDwh =>
                GenerateDataSourceAsync<ClickHouseTestDatabase>(dataGeneratorMetadata),
            DatabaseDependency.Postgres => GenerateDataSourceAsync<PostgresTestDatabase>(dataGeneratorMetadata),
            DatabaseDependency.SqlServer => GenerateDataSourceAsync<SqlServerTestDatabase>(dataGeneratorMetadata),
            DatabaseDependency.Oracle => GenerateDataSourceAsync<OracleTestDatabase>(dataGeneratorMetadata),
            DatabaseDependency.ApacheHive => GenerateEmptyDataSourceAsync(),
            DatabaseDependency.Ydb => GenerateDataSourceAsync<YdbTestDatabase>(dataGeneratorMetadata),
            _ => throw new ArgumentOutOfRangeException(nameof(dependencies), dependencies[0], null)
        };
    }

    private static async IAsyncEnumerable<Func<Task<object?[]?>>> GenerateDataSourceAsync<TDatabase>(
        DataGeneratorMetadata dataGeneratorMetadata)
    {
        var dataSource = new ClassDataSourceAttribute<TDatabase>
        {
            Shared = SharedType.PerTestSession
        };

        await foreach (var row in dataSource.GetDataRowsAsync(dataGeneratorMetadata).ConfigureAwait(false))
        {
            yield return row;
        }
    }

    private static async IAsyncEnumerable<Func<Task<object?[]?>>> GenerateEmptyDataSourceAsync()
    {
        yield return () => Task.FromResult<object?[]?>([]);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private static async IAsyncEnumerable<Func<Task<object?[]?>>> GenerateNoDataSourceAsync()
    {
        await Task.CompletedTask.ConfigureAwait(false);
        yield break;
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
            DatabaseDependency.SqlServer => TestCategories.SqlServer,
            DatabaseDependency.Oracle => TestCategories.Oracle,
            DatabaseDependency.ApacheHive => TestCategories.ApacheHive,
            DatabaseDependency.Ydb => TestCategories.Ydb,
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
            DatabaseDependency.SqlServer => new SqlServerParallelLimit(),
            DatabaseDependency.Oracle => new OracleParallelLimit(),
            DatabaseDependency.ApacheHive => new ApacheHiveParallelLimit(),
            DatabaseDependency.Ydb => new YdbParallelLimit(),
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
            !dependencies.Contains(DatabaseDependency.ApacheHive) ||
            TestEnvironment.IsCi)
        {
            return null;
        }

        return ApacheHiveDriver.IsInstalled
            ? null
            : "Apache Hive ODBC driver is not installed.";
    }

    private bool ShouldSkipOracle()
    {
        return SkipOracle && dependencies.Contains(DatabaseDependency.Oracle);
    }

    private static class ApacheHiveDriver
    {
        private static readonly Lazy<bool> Installed = new(Detect);

        public static bool IsInstalled => Installed.Value;

        private static bool Detect()
        {
            if (Environment.GetEnvironmentVariable("LOADER_TEST_APACHE_HIVE_ODBC") == "1")
            {
                return true;
            }

            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            return HasWindowsOdbcDriver(Microsoft.Win32.Registry.LocalMachine) ||
                   HasWindowsOdbcDriver(Microsoft.Win32.Registry.CurrentUser);
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static bool HasWindowsOdbcDriver(Microsoft.Win32.RegistryKey root)
        {
            try
            {
                using var key = root.OpenSubKey(@"SOFTWARE\ODBC\ODBCINST.INI\ODBC Drivers");
                return key?.GetValueNames()
                    .Any(static name => name.Contains("Hive", StringComparison.OrdinalIgnoreCase)) == true;
            }
            catch
            {
                return false;
            }
        }
    }
}

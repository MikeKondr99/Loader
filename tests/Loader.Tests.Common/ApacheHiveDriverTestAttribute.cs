namespace Loader.Tests.Common;

public sealed class ApacheHiveDriverTestAttribute :
    DriverTestAttribute<ApacheHiveParallelLimit>
{
    private static readonly Lazy<bool> HasDriver = new(DetectDriver);

    public ApacheHiveDriverTestAttribute()
        : base(TestCategories.ApacheHive)
    {
    }

    protected override string? GetSkipReason()
    {
        if (TestEnvironment.IsCi)
        {
            return null;
        }

        return HasDriver.Value
            ? null
            : "Apache Hive ODBC driver is not installed.";
    }

    private static bool DetectDriver()
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

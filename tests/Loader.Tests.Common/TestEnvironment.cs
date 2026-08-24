namespace Loader.Tests.Common;

public static class TestEnvironment
{
    public static bool IsCi => IsEnabled("LOADER_TEST_CI") ||
                               IsEnabled("CI") ||
                               IsEnabled("GITHUB_ACTIONS") ||
                               IsEnabled("TF_BUILD");

    public static bool ExternalDatabasesEnabled =>
        !IsDisabled("LOADER_TEST_EXTERNAL_DATABASES");

    private static bool IsEnabled(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return value is not null &&
               (value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("yes", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsDisabled(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return value is not null &&
               (value.Equals("0", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("no", StringComparison.OrdinalIgnoreCase));
    }
}

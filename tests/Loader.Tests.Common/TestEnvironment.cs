namespace Loader.Tests.Common;

public static class TestEnvironment
{
    public static bool IsCi => IsEnabled("LOADER_TEST_CI") ||
                               IsEnabled("CI") ||
                               IsEnabled("GITHUB_ACTIONS") ||
                               IsEnabled("TF_BUILD");

    private static bool IsEnabled(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return value is not null &&
               (value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("yes", StringComparison.OrdinalIgnoreCase));
    }
}

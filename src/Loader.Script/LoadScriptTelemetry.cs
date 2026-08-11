using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Loader.Script;

public static partial class LoadScriptTelemetry
{
    public static readonly ActivitySource ActivitySource = new("LoadScript");

    public static Activity? GetCurrentActivity()
    {
        return Activity.Current;
    }

    public static Activity SetSanitizedTag(this Activity activity, string key, object? value)
    {
        activity.SetTag(key, value is string text ? Sanitize(text) : value);
        return activity;
    }

    private static string Sanitize(string value)
    {
        return PasswordRegex().Replace(value, "$1=***");
    }

    [GeneratedRegex(@"(?i)\b(password|pwd)\s*=\s*[^;]+")]
    private static partial Regex PasswordRegex();
}

using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Loader.Script;

public static partial class LoadScriptTelemetry
{
    public static readonly ActivitySource ActivitySource = new("LoadScript");

    public static string RedactSource(string source)
    {
        return PasswordRegex().Replace(source, "$1=***");
    }

    [GeneratedRegex(@"(?i)\b(password|pwd)\s*=\s*[^;]+")]
    private static partial Regex PasswordRegex();
}

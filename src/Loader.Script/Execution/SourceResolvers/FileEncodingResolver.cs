using System.Text;
using Loader.Lang;
using Loader.Lang.Statements;

namespace Loader.Script;

/// <summary>
/// Преобразует текстовую script-опцию <c>encoding</c> в .NET Encoding для текстовых file provider-ов.
/// Принимаются только стандартные имена из .NET registry, например <c>utf-8</c>, <c>utf-16</c>, <c>windows-1251</c>.
/// </summary>
internal static class FileEncodingResolver
{
    private static readonly Lazy<IReadOnlyList<string>> SupportedNames = new(BuildSupportedNames);

    static FileEncodingResolver()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static Encoding? Optional(
        string providerName,
        string optionName,
        LoadOptionReader options,
        List<LangError> errors)
    {
        var option = options.GetOption(optionName);
        if (option is null)
        {
            return null;
        }

        var name = options.String(optionName);
        if (name is null)
        {
            return null;
        }

        try
        {
            return Encoding.GetEncoding(name.Trim());
        }
        catch (ArgumentException)
        {
            var message = AppendSuggestion(
                $"Provider '{providerName}' не поддерживает encoding '{name}'. Используйте стандартное имя .NET encoding.",
                name);
            errors.Add(new LangError
            {
                Message = message,
                Span = option.Span
            });
            return null;
        }
    }

    private static string[] BuildSupportedNames()
    {
        return Encoding.GetEncodings()
            .SelectMany(static info =>
            {
                var encoding = info.GetEncoding();
                return new[]
                {
                    info.Name,
                    encoding.WebName
                };
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string AppendSuggestion(string message, string name)
    {
        var suggestion = FindCodePageSuggestion(name) ?? NameSuggestion.FindClosest(name, SupportedNames.Value);
        return suggestion is null
            ? message
            : $"{message} Возможно вы имели в виду '{suggestion}'.";
    }

    private static string? FindCodePageSuggestion(string name)
    {
        var normalized = name.Trim().ToLowerInvariant();
        if (!normalized.StartsWith("cp", StringComparison.Ordinal) ||
            !int.TryParse(normalized[2..], out var codePage))
        {
            return null;
        }

        return Encoding.GetEncodings()
            .FirstOrDefault(info => info.CodePage == codePage)
            ?.Name;
    }
}

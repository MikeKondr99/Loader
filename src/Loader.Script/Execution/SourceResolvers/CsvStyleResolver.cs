using Loader.Lang;
using Loader.Lang.Statements;
using Sylvan.Data.Csv;

namespace Loader.Script;

/// <summary>
/// Преобразует script-опцию <c>style</c> в режим чтения CSV из Sylvan.
/// </summary>
internal static class CsvStyleResolver
{
    private static readonly IReadOnlyDictionary<string, CsvStyle> Styles =
        new Dictionary<string, CsvStyle>(StringComparer.OrdinalIgnoreCase)
        {
            ["standard"] = CsvStyle.Standard,
            ["lax"] = CsvStyle.Lax,
            ["escaped"] = CsvStyle.Escaped
        };

    public static CsvStyle Optional(
        string providerName,
        string optionName,
        LoadOptionReader options,
        List<LangError> errors,
        CsvStyle defaultValue)
    {
        var option = options.GetOption(optionName);
        if (option is null)
        {
            return defaultValue;
        }

        var name = options.String(optionName);
        if (name is null)
        {
            return defaultValue;
        }

        if (Styles.TryGetValue(name.Trim(), out var style))
        {
            return style;
        }

        var message = NameSuggestion.AppendSuggestion(
            $"Provider '{providerName}' не поддерживает style '{name}'. Используйте standard, lax или escaped.",
            name,
            Styles.Keys);
        errors.Add(new LangError
        {
            Message = message,
            Span = option.Span
        });
        return defaultValue;
    }
}

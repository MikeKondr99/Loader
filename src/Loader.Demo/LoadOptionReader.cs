using Loader.Lang.Expressions;
using Loader.Lang.Statements;

namespace Loader.Demo;

internal sealed class LoadOptionReader
{
    private readonly IReadOnlyDictionary<string, LoadOption> _options;

    public LoadOptionReader(IEnumerable<LoadOption> options)
    {
        _options = options.ToDictionary(static option => option.Name, StringComparer.OrdinalIgnoreCase);
    }

    public string? Provider => _options.Values
        .FirstOrDefault(static option => option.Value is null)
        ?.Name
        .ToLowerInvariant();

    public string? String(string name)
    {
        return GetValue(name) switch
        {
            null => null,
            StringLiteral value => value.Value,
            _ => throw new InvalidOperationException($"Опция '{name}' должна быть строкой.")
        };
    }

    public bool Boolean(string name, bool defaultValue)
    {
        return GetValue(name) switch
        {
            null => defaultValue,
            BooleanLiteral value => value.Value,
            _ => throw new InvalidOperationException($"Опция '{name}' должна быть boolean.")
        };
    }

    public char Character(string name, char defaultValue)
    {
        var value = String(name);
        if (value is null)
        {
            return defaultValue;
        }

        return value.Length == 1
            ? value[0]
            : throw new InvalidOperationException($"Опция '{name}' должна содержать один символ.");
    }

    private Literal? GetValue(string name)
    {
        return _options.TryGetValue(name, out var option) ? option.Value : null;
    }
}

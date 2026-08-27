using Loader.Lang;
using Loader.Lang.Expressions;
using Loader.Lang.Statements;

namespace Loader.Script;

/// <summary>
/// Утилита чтения options из provider call.
/// Централизует позиционные параметры, required-проверки, типизацию literal-ов и ошибки по неизвестным options.
/// </summary>
internal sealed class LoadOptionReader
{
    private readonly IReadOnlyList<LoadOption> _options;
    private readonly IReadOnlyDictionary<string, LoadOption> _optionsByName;
    private readonly List<LangError> _errors;

    public LoadOptionReader(IReadOnlyList<LoadOption> options, List<LangError> errors)
    {
        _options = options;
        _errors = errors;
        _optionsByName = BuildOptionMap(options, errors);
    }

    /// <summary>
    /// Количество options, переданных без имени: например <c>Csv('file.csv')</c>.
    /// </summary>
    public int PositionalCount => _options.Count(IsPositional);

    /// <summary>
    /// Возвращает options, которые еще не были сопоставлены с именованным provider-specific параметром.
    /// </summary>
    public IReadOnlyList<LoadOption> PositionalOptions()
    {
        return _options.Where(IsPositional).ToArray();
    }

    public LoadOptionReader MapPositionals(string providerName, ReadOnlySpan<string> names)
    {
        if (_options.All(static option => !IsPositional(option)))
        {
            return this;
        }

        var result = new List<LoadOption>(_options.Count);
        var positionalIndex = 0;
        var namedStarted = false;
        foreach (var option in _options)
        {
            if (!IsPositional(option))
            {
                namedStarted = true;
                result.Add(option);
                continue;
            }

            if (namedStarted)
            {
                _errors.Add(new LangError
                {
                    Message = $"Provider '{providerName}' принимает позиционные options только перед именованными.",
                    Span = option.Span
                });
                result.Add(option);
                continue;
            }

            if (positionalIndex >= names.Length)
            {
                _errors.Add(new LangError
                {
                    Message = $"Provider '{providerName}' не поддерживает позиционную option #{positionalIndex}.",
                    Span = option.Span
                });
                result.Add(option);
                positionalIndex++;
                continue;
            }

            result.Add(option with { Name = names[positionalIndex] });
            positionalIndex++;
        }

        return new LoadOptionReader(result, _errors);
    }

    public string? String(string name)
    {
        var option = GetOption(name);
        return option?.Value switch
        {
            null => null,
            StringLiteral value => value.Value,
            _ => AddError(option, $"Опция '{name}' должна быть строкой.")
        };
    }

    public string? Name(string name)
    {
        var option = GetOption(name);
        return option?.Value switch
        {
            null => null,
            NameLiteral value => value.Value,
            _ => AddError(option, $"Опция '{name}' должна быть именем.")
        };
    }

    public string? RequiredString(string name, LangSpan missingSpan, string missingMessage)
    {
        var option = GetOption(name);
        if (option is null)
        {
            _errors.Add(new LangError
            {
                Message = missingMessage,
                Span = missingSpan
            });
            return null;
        }

        return String(name);
    }

    public string? RequiredName(string name, LangSpan missingSpan, string missingMessage)
    {
        var option = GetOption(name);
        if (option is null)
        {
            _errors.Add(new LangError
            {
                Message = missingMessage,
                Span = missingSpan
            });
            return null;
        }

        return Name(name);
    }

    public bool Boolean(string name, bool defaultValue)
    {
        var option = GetOption(name);
        return option?.Value switch
        {
            null => defaultValue,
            BooleanLiteral value => value.Value,
            _ => AddError(option, $"Опция '{name}' должна быть true или false.", defaultValue)
        };
    }

    public long Integer(string name, long defaultValue)
    {
        var option = GetOption(name);
        return option?.Value switch
        {
            null => defaultValue,
            IntegerLiteral value => value.Value,
            _ => AddError(option, $"Опция '{name}' должна быть целым числом.", defaultValue)
        };
    }

    public long? RequiredInteger(string name, LangSpan missingSpan, string missingMessage)
    {
        var option = GetOption(name);
        if (option is null)
        {
            _errors.Add(new LangError
            {
                Message = missingMessage,
                Span = missingSpan
            });
            return null;
        }

        return option.Value switch
        {
            IntegerLiteral value => value.Value,
            _ => AddIntegerError(option, $"Опция '{name}' должна быть целым числом.")
        };
    }

    public char Character(string name, char defaultValue)
    {
        var value = String(name);
        if (value is null)
        {
            return defaultValue;
        }

        if (value.Length == 1)
        {
            return value[0];
        }

        AddError(GetOption(name), $"Опция '{name}' должна содержать один символ.");
        return defaultValue;
    }

    public LoadOption? GetOption(string name)
    {
        return _optionsByName.TryGetValue(name, out var option) ? option : null;
    }

    public void RejectUnknownOptions(string providerName, ReadOnlySpan<string> allowedNames)
    {
        foreach (var option in _options)
        {
            if (Contains(allowedNames, option.Name))
            {
                continue;
            }

            _errors.Add(new LangError
            {
                Message = $"Опция '{option.Name}' не поддерживается provider-ом '{providerName}'.",
                Span = option.Span
            });
        }
    }

    private static bool Contains(ReadOnlySpan<string> values, string value)
    {
        foreach (var item in values)
        {
            if (string.Equals(item, value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPositional(LoadOption option)
    {
        return int.TryParse(option.Name, out _);
    }

    private static IReadOnlyDictionary<string, LoadOption> BuildOptionMap(
        IReadOnlyList<LoadOption> options,
        List<LangError> errors)
    {
        var map = new Dictionary<string, LoadOption>(StringComparer.OrdinalIgnoreCase);
        foreach (var option in options)
        {
            if (map.TryAdd(option.Name, option))
            {
                continue;
            }

            errors.Add(new LangError
            {
                Message = $"Опция '{option.Name}' указана несколько раз.",
                Span = option.Span
            });
        }

        return map;
    }

    private string? AddError(LoadOption? option, string message)
    {
        if (option is not null)
        {
            _errors.Add(new LangError
            {
                Message = message,
                Span = option.Span
            });
        }

        return null;
    }

    private bool AddError(LoadOption option, string message, bool fallback)
    {
        _errors.Add(new LangError
        {
            Message = message,
            Span = option.Span
        });
        return fallback;
    }

    private long AddError(LoadOption option, string message, long fallback)
    {
        _errors.Add(new LangError
        {
            Message = message,
            Span = option.Span
        });
        return fallback;
    }

    private long? AddIntegerError(LoadOption option, string message)
    {
        _errors.Add(new LangError
        {
            Message = message,
            Span = option.Span
        });
        return null;
    }
}

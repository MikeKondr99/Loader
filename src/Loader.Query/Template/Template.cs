namespace Loader.Query.Template;

/// <summary>
/// Минимальная модель SQL-шаблона без compiler-specific логики.
/// </summary>
public readonly record struct Template : ITemplate
{
    public required IReadOnlyList<ITemplateToken> Tokens { get; init; }

    public static Template Text(string text)
    {
        return new Template
        {
            Tokens = [new ConstToken(text)]
        };
    }

    public static Template FromTokens(IEnumerable<ITemplateToken> tokens)
    {
        return new Template
        {
            Tokens = tokens.ToArray()
        };
    }

    public override string ToString()
    {
        return string.Concat(Tokens.Select(static token => token switch
        {
            ConstToken constToken => constToken.Text,
            ArgToken argToken => $"{{{argToken.Index}}}",
            _ => throw new ArgumentOutOfRangeException(nameof(token), token, null)
        }));
    }
}

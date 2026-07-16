using System.Runtime.CompilerServices;

namespace Loader.Query.Template;

[InterpolatedStringHandler]
public struct TemplateInterpolatedStringHandler
{
    public List<ITemplateToken> Tokens { get; set; }

    public TemplateInterpolatedStringHandler(int literalLength, int formattedCount)
    {
        Tokens = new List<ITemplateToken>(literalLength + formattedCount);
    }

    public void AppendLiteral(string text)
    {
        Tokens.Add(new ConstToken(text));
    }

    public void AppendFormatted(ITemplate? template)
    {
        if (template is not null)
        {
            Tokens.AddRange(template.Tokens);
        }
    }

    public void AppendFormatted<T>(T value)
    {
        if (value is int index)
        {
            Tokens.Add(new ArgToken(index));
            return;
        }

        if (value is string text)
        {
            Tokens.Add(new ConstToken(text));
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(value), value, $"Template supports only {nameof(Int32)}, {nameof(String)} or {nameof(ITemplate)} formatted values.");
    }

    public Template Compile()
    {
        return new Template
        {
            Tokens = Tokens
        };
    }

    public static implicit operator TemplateInterpolatedStringHandler(string text)
    {
        return new TemplateInterpolatedStringHandler
        {
            Tokens = [new ConstToken(text)]
        };
    }
}

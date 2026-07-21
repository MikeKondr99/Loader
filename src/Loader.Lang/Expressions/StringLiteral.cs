namespace Loader.Lang.Expressions;

public sealed record StringLiteral(string Value) : Literal<string>(Value)
{
    public StringLiteral(string value, LangSpan span)
        : this(value)
    {
        Span = span;
    }

    public override string ToString()
    {
        return $"'{Value}'";
    }
}

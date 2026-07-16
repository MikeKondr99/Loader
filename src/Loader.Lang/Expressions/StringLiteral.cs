namespace Loader.Lang.Expressions;

public sealed record StringLiteral(string Value) : Literal<string>(Value)
{
    public StringLiteral(string value, ExprSpan span)
        : this(value)
    {
        Span = span;
    }

    public override string ToString()
    {
        return $"'{Value}'";
    }
}

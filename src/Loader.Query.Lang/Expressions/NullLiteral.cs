namespace Loader.Query.Lang.Expressions;

public sealed record NullLiteral : Literal
{
    public override string ToString()
    {
        return "null";
    }
}

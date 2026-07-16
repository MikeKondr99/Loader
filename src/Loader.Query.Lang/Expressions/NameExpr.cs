namespace Loader.Query.Lang.Expressions;

public sealed record NameExpr(string Value) : Expr
{
    public override string ToString()
    {
        return $"[{Value}]";
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(nameof(NameExpr), Value);
    }
}

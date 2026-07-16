namespace Loader.Lang.Expressions;

public sealed record BooleanLiteral(bool Value) : Literal<bool>(Value)
{
    public override string ToString()
    {
        return Value ? "true" : "false";
    }
}

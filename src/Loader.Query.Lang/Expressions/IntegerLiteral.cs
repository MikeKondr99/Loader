using System.Globalization;

namespace Loader.Query.Lang.Expressions;

public sealed record IntegerLiteral(long Value) : Literal<long>(Value)
{
    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}

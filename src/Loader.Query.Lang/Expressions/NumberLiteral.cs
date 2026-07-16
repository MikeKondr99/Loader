using System.Globalization;

namespace Loader.Query.Lang.Expressions;

public sealed record NumberLiteral(double Value) : Literal<double>(Value)
{
    public override string ToString()
    {
        return Value.ToString("0.0###############", CultureInfo.InvariantCulture);
    }
}

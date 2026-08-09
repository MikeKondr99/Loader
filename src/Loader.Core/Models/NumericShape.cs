namespace Loader.Core.Models;

internal static class NumericShape
{
    public static (int? Precision, int? Scale) Normalize(int? precision, int? scale)
    {
        if (precision is null or <= 0)
        {
            return (null, null);
        }

        if (scale is < 0)
        {
            return (precision, null);
        }

        return (precision, scale);
    }
}

using System.Globalization;

namespace Loader.Lang;

internal static class NumericLiteralText
{
    public static long ParseInteger(string text, LangSpan span)
    {
        Validate(text, span);
        return long.Parse(Normalize(text), CultureInfo.InvariantCulture);
    }

    public static double ParseNumber(string text, LangSpan span)
    {
        Validate(text, span);
        return double.Parse(Normalize(text), CultureInfo.InvariantCulture);
    }

    public static string Normalize(string text)
    {
        return text.Replace("_", string.Empty, StringComparison.Ordinal);
    }

    private static void Validate(string text, LangSpan span)
    {
        if (text.EndsWith('_') ||
            text.EndsWith('.') ||
            text.Contains("_.", StringComparison.Ordinal) ||
            text.Contains("._", StringComparison.Ordinal))
        {
            throw new LangErrorException(new FormatException($"Invalid numeric literal '{text}'."))
            {
                Error = new LangError
                {
                    Span = span,
                    Message = $"Некорректный числовой литерал '{text}'. Символ '_' можно использовать только внутри числа между цифрами."
                }
            };
        }
    }
}

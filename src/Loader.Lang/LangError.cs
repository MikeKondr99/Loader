namespace Loader.Lang;

/// <summary>
/// Ошибка парсинга или будущего semantic resolve в языке Loader.
/// </summary>
public sealed record LangError
{
    public required LangSpan Span { get; init; }

    public required string Message { get; init; }
}

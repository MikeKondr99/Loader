namespace Loader.Query.Lang;

/// <summary>
/// Ошибка парсинга или будущего разрешения выражения.
/// </summary>
public sealed record ExprError
{
    public required ExprSpan Span { get; init; }

    public required string Message { get; init; }
}

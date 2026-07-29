namespace Loader.Lang.Statements;

public sealed record LimitPart
{
    public required long Value { get; init; }

    public required LangSpan Span { get; init; }
}

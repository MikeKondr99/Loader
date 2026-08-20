namespace Loader.Lang.Statements;

public sealed record FirstPart
{
    public required long Value { get; init; }

    public required LangSpan Span { get; init; }
}

namespace Loader.Lang.Statements;

public sealed record SourcePart
{
    public required string Value { get; init; }

    public required LangSpan Span { get; init; }
}

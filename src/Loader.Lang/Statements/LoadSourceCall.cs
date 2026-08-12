namespace Loader.Lang.Statements;

public sealed record LoadSourceCall
{
    public required string Name { get; init; }

    public required LangSpan NameSpan { get; init; }

    public required List<LoadOption> Options { get; init; }

    public InlineData? InlineData { get; init; }

    public required LangSpan Span { get; init; }
}

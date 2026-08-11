namespace Loader.Lang.Statements;

public sealed record DropStatement : Statement
{
    public LangSpan? DropSpan { get; init; }

    public required string Name { get; init; }

    public required LangSpan NameSpan { get; init; }
}

namespace Loader.Demo;

internal sealed record CompiledLoadQuery
{
    public required string Sql { get; init; }

    public required IReadOnlyList<string> LogicalNames { get; init; }
}

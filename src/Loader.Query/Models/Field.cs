using Loader.Query.Template;

namespace Loader.Query.Models;

/// <summary>
/// Поле, доступное в query source или полученное на выходе query.
/// </summary>
public sealed record Field
{
    public required string Alias { get; init; }

    public required ITemplate Template { get; init; }

    public required FieldType Type { get; init; }
}

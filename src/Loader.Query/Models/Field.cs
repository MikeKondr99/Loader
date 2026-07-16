namespace Loader.Query.Models;

/// <summary>
/// Поле, доступное в source или полученное на выходе LOAD-запроса.
/// </summary>
public sealed record Field
{
    public required string Name { get; init; }

    public required FieldType Type { get; init; }
}

namespace Loader.Core.Data;

/// <summary>
/// Описание одного поля в нормализованной схеме данных.
/// </summary>
public sealed record DataField
{
    public required int Ordinal { get; init; }

    public required string Name { get; init; }

    public required DataType DataType { get; init; }
}

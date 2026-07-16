namespace Loader.Query.Models;

/// <summary>
/// Тип поля в query layer: доменный тип плюс nullability.
/// </summary>
public sealed record FieldType
{
    public required DataType DataType { get; init; }

    public required bool CanBeNull { get; init; }
}

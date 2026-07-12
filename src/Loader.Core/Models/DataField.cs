namespace Loader.Core.Models;

/// <summary>
/// Описание одного поля в нормализованной схеме данных.
/// </summary>
public sealed record DataField
{
    public required int Ordinal { get; init; }

    public required string Name { get; init; }

    public required DataType DataType { get; init; }

    public required Type ClrType { get; init; }

    public bool? AllowDBNull { get; init; }

    public int? ColumnSize { get; init; }

    public int? NumericPrecision { get; init; }

    public int? NumericScale { get; init; }

    public required Func<object, object>? Convert { get; init; }

    public required bool ReadValue { get; init; }
}

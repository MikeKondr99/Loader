namespace Loader.Core.Data;

/// <summary>
/// Описание того, как CLR-тип читается в доменной схеме Loader.
/// </summary>
public sealed class DataValueMapping
{
    public required DataType DataType { get; init; }

    public required Type ClrType { get; init; }

    public required Func<object, object>? Convert { get; init; }

    public required bool ReadValue { get; init; }

    public bool RequiresConversion => Convert is not null;
}

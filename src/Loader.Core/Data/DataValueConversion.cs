namespace Loader.Core.Data;

/// <summary>
/// Описание того, как провайдерский CLR-тип сводится к типу библиотеки.
/// </summary>
public sealed class DataValueConversion
{
    public required DataType DataType { get; init; }

    public required Type CanonicalClrType { get; init; }

    public required Func<object, object> Convert { get; init; }
}

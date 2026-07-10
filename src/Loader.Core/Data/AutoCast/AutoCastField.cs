namespace Loader.Core.Data.AutoCast;

/// <summary>
/// Описание автокаста одного поля по имени.
/// </summary>
public sealed record AutoCastField
{
    public required string Name { get; init; }

    public required IAutoCastFormat Format { get; init; }
}

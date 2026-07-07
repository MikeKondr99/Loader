namespace Loader.Core.Data;

/// <summary>
/// Настройки нормализации reader-а перед переходом в доменную модель Loader.
/// </summary>
public sealed record NormalizeConfig
{
    public required int? Limit { get; init; }
}

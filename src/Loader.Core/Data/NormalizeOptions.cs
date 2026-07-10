namespace Loader.Core.Data;

/// <summary>
/// Настройки нормализации DbDataReader в доменный reader.
/// </summary>
public sealed record NormalizeOptions
{
    public bool Buffer { get; init; } = true;
}

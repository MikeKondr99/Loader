namespace Loader.Core.Data;

/// <summary>
/// Настройки сбора метаинформации по stream-данным.
/// </summary>
public sealed record DataMetaOptions
{
    public static DataMetaOptions Default { get; } = new();

    public int? MaxCardinality { get; init; } = 20;
}

using Loader.Core.Abstractions;

namespace Loader.Core.Providers.Qvd;

/// <summary>
/// Настройки чтения одной таблицы из QVD-файла.
/// </summary>
public sealed record QvdTableConfig : ITableConfig
{
    public required string FileName { get; init; }
}

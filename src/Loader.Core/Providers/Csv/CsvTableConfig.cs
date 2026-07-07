using System.Text;
using Loader.Core.Abstractions;

namespace Loader.Core.Providers.Csv;

/// <summary>
/// Настройки чтения одной CSV-таблицы из файлового source.
/// </summary>
public sealed record CsvTableConfig : ITableConfig
{
    public required string FileName { get; init; }

    public char Delimiter { get; init; } = ',';

    public bool HasHeader { get; init; } = true;

    public Encoding? Encoding { get; init; }
}

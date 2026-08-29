using System.Text;
using Loader.Core.Abstractions;
using Sylvan.Data.Csv;

namespace Loader.Core.Providers.Csv;

/// <summary>
/// Настройки чтения одной CSV-таблицы из файлового source.
/// </summary>
public sealed record CsvTableConfig : ITableConfig
{
    public required string FileName { get; init; }

    public char Delimiter { get; init; } = ',';

    public bool HasHeader { get; init; } = true;

    public long SkipRows { get; init; }

    public CsvStyle Style { get; init; } = CsvStyle.Lax;

    public Encoding? Encoding { get; init; }

    public char? Comment { get; init; }

    public bool TrimHeaders { get; init; }

    public bool TrimValues { get; init; }

    public bool EmptyAsNull { get; init; }
}

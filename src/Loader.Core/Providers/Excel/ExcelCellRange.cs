using System.Globalization;

namespace Loader.Core.Providers.Excel;

/// <summary>
/// Прямоугольный диапазон ячеек Excel в A1-нотации, например <c>B2:D10</c>, <c>B100:D</c> или <c>B:D</c>.
/// Координаты 1-based: строка 1 и колонка 1 соответствуют ячейке <c>A1</c>.
/// </summary>
public sealed record ExcelCellRange
{
    private const int MaxRow = 1_048_576;
    private const int MaxColumn = 16_384;

    /// <summary>
    /// Первая строка диапазона.
    /// </summary>
    public required int StartRow { get; init; }

    /// <summary>
    /// Первая колонка диапазона.
    /// </summary>
    public required int StartColumn { get; init; }

    /// <summary>
    /// Последняя строка диапазона. Если не задана, range читается до конца данных листа.
    /// </summary>
    public int? EndRow { get; init; }

    /// <summary>
    /// Последняя колонка диапазона.
    /// </summary>
    public required int EndColumn { get; init; }

    /// <summary>
    /// Количество колонок в диапазоне.
    /// </summary>
    public int ColumnCount => EndColumn - StartColumn + 1;

    public static bool TryParse(string text, out ExcelCellRange? range)
    {
        range = null;
        var parts = text.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 ||
            !TryParseReference(parts[0], out var startReference) ||
            !TryParseReference(parts[1], out var endReference))
        {
            return false;
        }

        var startRow = startReference.Row ?? 1;
        var endRow = endReference.Row;
        if (startRow > MaxRow ||
            (endRow is not null && (endRow.Value > MaxRow || startRow > endRow.Value)) ||
            startReference.Column > endReference.Column)
        {
            return false;
        }

        range = new ExcelCellRange
        {
            StartRow = startRow,
            StartColumn = startReference.Column,
            EndRow = endRow,
            EndColumn = endReference.Column
        };
        return true;
    }

    internal static string GetColumnName(int ordinal)
    {
        var value = ordinal + 1;
        var chars = new Stack<char>();

        while (value > 0)
        {
            value--;
            chars.Push((char)('A' + value % 26));
            value /= 26;
        }

        return new string(chars.ToArray());
    }

    private static bool TryParseReference(string text, out CellReference reference)
    {
        reference = new CellReference(null, 0);

        var index = 0;
        while (index < text.Length && char.IsAsciiLetter(text[index]))
        {
            reference = reference with
            {
                Column = reference.Column * 26 + (char.ToUpperInvariant(text[index]) - 'A' + 1)
            };
            if (reference.Column > MaxColumn)
            {
                return false;
            }

            index++;
        }

        if (index == 0)
        {
            return false;
        }

        var rowText = text[index..];
        if (rowText.Length == 0)
        {
            return reference.Column > 0;
        }

        if (rowText.Any(static ch => !char.IsAsciiDigit(ch)) ||
            !int.TryParse(rowText, NumberStyles.None, CultureInfo.InvariantCulture, out var row) ||
            row <= 0 ||
            row > MaxRow ||
            reference.Column <= 0)
        {
            return false;
        }

        reference = reference with { Row = row };
        return true;
    }

    private sealed record CellReference(int? Row, int Column);
}

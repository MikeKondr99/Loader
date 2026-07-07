using System.Data.Common;
using Loader.Core.Data;
using Sylvan.Data.Csv;

namespace Loader.Core.Providers.Csv;

/// <summary>
/// CSV-specific reader wrapper над Sylvan reader-ом.
/// </summary>
/// <remarks>
/// Этот wrapper фиксирует контракт Loader поверх поведения Sylvan:
///
/// - Если CSV читается без header, имена колонок генерируются как в Excel:
///   <c>A</c>, <c>B</c>, ... <c>Z</c>, <c>AA</c>, <c>AB</c>.
/// - Если в строке меньше значений, чем в схеме, отсутствующие значения возвращаются как <see cref="DBNull"/>.
/// - Если в строке больше значений, чем в схеме, лишние значения остаются недоступны через <see cref="DbDataReader"/> и игнорируются.
/// - Ошибки формата CSV от Sylvan нормализуются в <see cref="MalformedCsvProviderException"/>.
///
/// Остальное поведение остается provider-native и делегируется исходному reader-у через <see cref="DbDataReaderDecorator"/>.
/// </remarks>
internal sealed class CsvProviderDataReader : DbDataReaderDecorator
{
    private readonly string _fileName;
    private readonly CsvDataReader? _csvReader;
    private readonly bool _useGeneratedColumnNames;

    public CsvProviderDataReader(DbDataReader inner, string fileName, bool useGeneratedColumnNames)
        : base(inner)
    {
        _fileName = fileName;
        _csvReader = inner as CsvDataReader;
        _useGeneratedColumnNames = useGeneratedColumnNames;
    }

    public override string GetName(int ordinal)
    {
        return _useGeneratedColumnNames
            ? GetExcelColumnName(ordinal)
            : Inner.GetName(ordinal);
    }

    public override bool Read()
    {
        try
        {
            return Inner.Read();
        }
        catch (CsvFormatException ex)
        {
            throw new MalformedCsvProviderException(_fileName, ex);
        }
    }

    public override bool IsDBNull(int ordinal)
    {
        return IsMissingRowValue(ordinal) || Inner.IsDBNull(ordinal);
    }

    public override object GetValue(int ordinal)
    {
        return IsMissingRowValue(ordinal)
            ? DBNull.Value
            : Inner.GetValue(ordinal);
    }

    public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await Inner.ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (CsvFormatException ex)
        {
            throw new MalformedCsvProviderException(_fileName, ex);
        }
    }

    private bool IsMissingRowValue(int ordinal)
    {
        return _csvReader is not null && ordinal >= _csvReader.RowFieldCount;
    }

    private static string GetExcelColumnName(int ordinal)
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
}

using System.Data.Common;
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
    private readonly bool _trimHeaders;
    private readonly bool _trimValues;
    private readonly bool _emptyAsNull;

    public CsvProviderDataReader(
        DbDataReader inner,
        string fileName,
        bool useGeneratedColumnNames,
        bool trimHeaders,
        bool trimValues,
        bool emptyAsNull)
        : base(inner)
    {
        _fileName = fileName;
        _csvReader = inner as CsvDataReader;
        _useGeneratedColumnNames = useGeneratedColumnNames;
        _trimHeaders = trimHeaders;
        _trimValues = trimValues;
        _emptyAsNull = emptyAsNull;
    }

    public override string GetName(int ordinal)
    {
        var name = _useGeneratedColumnNames
            ? GetExcelColumnName(ordinal)
            : Inner.GetName(ordinal);
        return _trimHeaders && !_useGeneratedColumnNames
            ? name.Trim()
            : name;
    }

    public override int GetOrdinal(string name)
    {
        for (var ordinal = 0; ordinal < FieldCount; ordinal++)
        {
            if (string.Equals(GetName(ordinal), name, StringComparison.Ordinal))
            {
                return ordinal;
            }
        }

        throw new IndexOutOfRangeException($"Column '{name}' was not found.");
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
        if (IsMissingRowValue(ordinal) || Inner.IsDBNull(ordinal))
        {
            return true;
        }

        if (!_emptyAsNull)
        {
            return false;
        }

        var value = Inner.GetString(ordinal);
        return _trimValues
            ? string.IsNullOrWhiteSpace(value)
            : value.Length == 0;
    }

    public override object GetValue(int ordinal)
    {
        if (IsMissingRowValue(ordinal))
        {
            return DBNull.Value;
        }

        var value = Inner.GetValue(ordinal);
        if (value == DBNull.Value || value is not string text)
        {
            return value;
        }

        return NormalizeTextValue(text);
    }

    public override int GetValues(object[] values)
    {
        var count = Math.Min(values.Length, FieldCount);
        for (var ordinal = 0; ordinal < count; ordinal++)
        {
            values[ordinal] = GetValue(ordinal);
        }

        return count;
    }

    public override string GetString(int ordinal)
    {
        if (IsMissingRowValue(ordinal) || Inner.IsDBNull(ordinal))
        {
            throw new InvalidCastException($"Column '{GetName(ordinal)}' is null.");
        }

        var value = NormalizeTextValue(Inner.GetString(ordinal));
        if (value == DBNull.Value)
        {
            throw new InvalidCastException($"Column '{GetName(ordinal)}' is null.");
        }

        return (string)value;
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

    private object NormalizeTextValue(string text)
    {
        if (_trimValues)
        {
            text = text.Trim();
        }

        return _emptyAsNull && text.Length == 0
            ? DBNull.Value
            : text;
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

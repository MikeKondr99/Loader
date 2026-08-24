using System.Data;
using System.Data.Common;
using System.Globalization;

namespace Loader.Core.Providers.Odbc;

/// <summary>
/// Нормализует временные ODBC-колонки до того, как общая нормализация Loader построит схему reader-а.
/// </summary>
/// <remarks>
/// ADO.NET ODBC-драйверы не единообразны в CLR-типах date/time: date может прийти как
/// <see cref="DateTime"/>, time как <see cref="TimeSpan"/> или <see cref="DateTime"/>, а offset-aware
/// временные значения как <see cref="DateTimeOffset"/>, <see cref="DateTime"/> или текст. Loader строит
/// <see cref="DataSchema"/> по CLR-типу, поэтому adapter использует ODBC provider type names и отдаёт
/// стабильные CLR-формы для распространённых временных случаев.
/// </remarks>
public sealed class OdbcTemporalDataReader : DbDataReaderDecorator
{
    /// <summary>
    /// Создаёт reader, который делегирует ODBC reader-у и корректирует временные типы и значения.
    /// </summary>
    /// <param name="inner">Открытый ODBC data reader.</param>
    public OdbcTemporalDataReader(DbDataReader inner)
        : base(inner)
    {
    }

    /// <summary>
    /// Возвращает скорректированные CLR-типы для ODBC date, time и timezone-aware колонок.
    /// </summary>
    public override Type GetFieldType(int ordinal)
    {
        return GetTemporalKind(ordinal) switch
        {
            OdbcTemporalKind.Date => typeof(DateOnly),
            OdbcTemporalKind.Time => typeof(TimeOnly),
            OdbcTemporalKind.TimeZone => typeof(string),
            _ => Inner.GetFieldType(ordinal)
        };
    }

    /// <summary>
    /// Возвращает скорректированные значения, соответствующие <see cref="GetFieldType"/>, для временных ODBC-колонок.
    /// </summary>
    public override object GetValue(int ordinal)
    {
        var value = Inner.GetValue(ordinal);
        if (value is null || value == DBNull.Value)
        {
            return DBNull.Value;
        }

        return GetTemporalKind(ordinal) switch
        {
            OdbcTemporalKind.Date => ConvertDate(value),
            OdbcTemporalKind.Time => ConvertTime(value),
            OdbcTemporalKind.TimeZone => ConvertTimeZone(value),
            _ => value
        };
    }

    /// <summary>
    /// Читает значения через <see cref="GetValue"/>, чтобы временные корректировки применялись единообразно.
    /// </summary>
    public override int GetValues(object[] values)
    {
        var count = Math.Min(values.Length, FieldCount);
        for (var i = 0; i < count; i++)
        {
            values[i] = GetValue(i);
        }

        return count;
    }

    /// <summary>
    /// Отражает скорректированные CLR-типы в schema table, которую читают последующие readers метаданных.
    /// </summary>
    public override DataTable? GetSchemaTable()
    {
        var table = Inner.GetSchemaTable();
        if (table is null || !table.Columns.Contains(SchemaTableColumn.DataType))
        {
            return table;
        }

        table = table.Copy();
        table.Columns[SchemaTableColumn.DataType]!.ReadOnly = false;

        foreach (DataRow row in table.Rows)
        {
            if (row[SchemaTableColumn.ColumnOrdinal] is not int ordinal)
            {
                continue;
            }

            row[SchemaTableColumn.DataType] = GetFieldType(ordinal);
        }

        return table;
    }

    private OdbcTemporalKind GetTemporalKind(int ordinal)
    {
        return OdbcTemporalClassifier.Classify(GetDataTypeName(ordinal), Inner.GetFieldType(ordinal));
    }

    private static object ConvertDate(object value)
    {
        return value switch
        {
            DateOnly date => date,
            DateTime dateTime => DateOnly.FromDateTime(dateTime),
            DateTimeOffset dateTimeOffset => DateOnly.FromDateTime(dateTimeOffset.DateTime),
            string text when DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) => date,
            string text when DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime) => DateOnly.FromDateTime(dateTime),
            _ => value
        };
    }

    private static object ConvertTime(object value)
    {
        return value switch
        {
            TimeOnly time => time,
            TimeSpan timeSpan => TimeOnly.FromTimeSpan(timeSpan),
            DateTime dateTime => TimeOnly.FromDateTime(dateTime),
            DateTimeOffset dateTimeOffset => TimeOnly.FromDateTime(dateTimeOffset.DateTime),
            string text when TimeOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) => time,
            string text when TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var timeSpan) => TimeOnly.FromTimeSpan(timeSpan),
            _ => value
        };
    }

    private static object ConvertTimeZone(object value)
    {
        return value switch
        {
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            TimeOnly time => time.ToString("O", CultureInfo.InvariantCulture),
            TimeSpan timeSpan => timeSpan.ToString("c", CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }
}

using System.Data;
using System.Data.Common;
using System.Globalization;
using Loader.Core.Providers.Odbc;
using Loader.Core.Tests.Infrastructure;

namespace Loader.Core.Tests;

public sealed class OdbcTemporalDataReaderTests
{
    [Test]
    [DisplayName("ODBC temporal reader выравнивает date time timestamp и timezone значения")]
    public async Task Normalizes_odbc_temporal_values()
    {
        using var table = new DataTable();
        table.Columns.Add("date_value", typeof(DateTime));
        table.Columns.Add("time_value", typeof(TimeSpan));
        table.Columns.Add("timestamp_value", typeof(DateTime));
        table.Columns.Add("timezone_value", typeof(DateTimeOffset));

        var timestamp = new DateTime(2026, 1, 2, 3, 4, 5);
        var offset = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(3));
        table.Rows.Add(
            new DateTime(2026, 1, 2),
            new TimeSpan(3, 4, 5),
            timestamp,
            offset);

        using var rawReader = table.CreateDataReader();
        using var typedReader = new TypeNameDataReader(
            rawReader,
            [
                "SQL_TYPE_DATE",
                "SQL_TYPE_TIME",
                "SQL_TYPE_TIMESTAMP",
                "TIMESTAMP WITH TIME ZONE"
            ]);
        using var reader = new OdbcTemporalDataReader(typedReader).Normalize();

        await Assert.That(reader).HaveData(
            columns: ["date_value", "time_value", "timestamp_value", "timezone_value"],
            types: [DataType.Date, DataType.Time, DataType.DateTime, DataType.Text],
            rows: [
                (
                    new DateOnly(2026, 1, 2),
                    new TimeOnly(3, 4, 5),
                    timestamp,
                    offset.ToString("O", CultureInfo.InvariantCulture)
                )
            ]);
    }

    [Test]
    [DisplayName("ODBC temporal reader выравнивает строковые date и time значения")]
    public async Task Normalizes_odbc_temporal_string_values()
    {
        using var table = new DataTable();
        table.Columns.Add("date_value", typeof(string));
        table.Columns.Add("time_value", typeof(string));

        table.Rows.Add("2026-01-02", "03:04:05");

        using var rawReader = table.CreateDataReader();
        using var typedReader = new TypeNameDataReader(rawReader, ["DATE", "TIME"]);
        using var reader = new OdbcTemporalDataReader(typedReader).Normalize();

        await Assert.That(reader).HaveData(
            columns: ["date_value", "time_value"],
            types: [DataType.Date, DataType.Time],
            rows: [
                (
                    new DateOnly(2026, 1, 2),
                    new TimeOnly(3, 4, 5)
                )
            ]);
    }

    [Test]
    [MethodDataSource(nameof(TemporalKindCases))]
    [DisplayName("ODBC temporal classifier определяет временной тип по имени типа драйвера")]
    public async Task Classifies_odbc_temporal_type_names(string typeName, Type fieldType, OdbcTemporalKind expected)
    {
        await Assert.That(OdbcTemporalClassifier.Classify(typeName, fieldType)).IsEqualTo(expected);
    }

    public static IEnumerable<(string TypeName, Type FieldType, OdbcTemporalKind Expected)> TemporalKindCases()
    {
        yield return ("SQL_TYPE_DATE", typeof(DateTime), OdbcTemporalKind.Date);
        yield return ("DATE", typeof(DateTime), OdbcTemporalKind.Date);
        yield return ("SQL_TYPE_TIME", typeof(TimeSpan), OdbcTemporalKind.Time);
        yield return ("TIME", typeof(DateTime), OdbcTemporalKind.Time);
        yield return ("SQL_TYPE_TIMESTAMP", typeof(DateTime), OdbcTemporalKind.None);
        yield return ("TIMESTAMP", typeof(DateTime), OdbcTemporalKind.None);
        yield return ("TIMESTAMP WITH TIME ZONE", typeof(DateTimeOffset), OdbcTemporalKind.TimeZone);
        yield return ("TIMESTAMP WITHOUT TIME ZONE", typeof(DateTime), OdbcTemporalKind.None);
        yield return ("TIME WITHOUT TIME ZONE", typeof(TimeSpan), OdbcTemporalKind.Time);
        yield return ("datetimeoffset", typeof(DateTimeOffset), OdbcTemporalKind.TimeZone);
        yield return ("timestamptz", typeof(DateTime), OdbcTemporalKind.TimeZone);
        yield return ("varchar", typeof(string), OdbcTemporalKind.None);
    }

    private sealed class TypeNameDataReader : DbDataReaderDecorator
    {
        private readonly IReadOnlyList<string> _typeNames;

        public TypeNameDataReader(DbDataReader inner, IReadOnlyList<string> typeNames)
            : base(inner)
        {
            _typeNames = typeNames;
        }

        public override string GetDataTypeName(int ordinal)
        {
            return _typeNames[ordinal];
        }
    }
}
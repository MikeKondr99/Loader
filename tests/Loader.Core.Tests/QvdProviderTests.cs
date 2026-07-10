using Loader.Core.Data;
using Loader.Core.Providers.Qvd;
using Loader.Core.Sources;
using Loader.Core.Tests.Infrastructure;

namespace Loader.Core.Tests;

public sealed class QvdProviderTests
{
    private static readonly QvdProvider Provider = new();
    private static readonly FileSystemSource Source = new(GetFixtureRoot());

    [Test]
    [DisplayName("QVD basic text читает строки как Text")]
    public async Task Basic_text_reads_rows_as_text()
    {
        await using var rawReader = await OpenAsync("basic_text.qvd");
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "name", "city"],
            types: [DataType.Text, DataType.Text, DataType.Text],
            rows: [
                ("1", "Alice", "Moscow"),
                ("2", "Bob", "Berlin"),
                ("3", "Carol", "Warsaw")
            ]);
    }

    [Test]
    [DisplayName("QVD primitive types читает text int number bool-text")]
    public async Task Primitive_types_read_expected_canonical_types()
    {
        await using var rawReader = await OpenAsync("primitive_types.qvd");
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["text_value", "int_value", "num_value", "bool_text"],
            types: [DataType.Text, DataType.Integer, DataType.Number, DataType.Text],
            rows: [
                ("hello", 42, 3.14d, "true"),
                ("world", -7, 0.5d, "false"),
                ("qlik", 1000, -12.75d, "true")
            ]);
    }

    [Test]
    [DisplayName("QVD date time timestamp dual values нормализуются в дату время datetime")]
    public async Task Date_time_dual_values_read_as_temporal_types()
    {
        await using var rawReader = await OpenAsync("date_time.qvd");
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["date_value", "time_value", "timestamp_value"],
            types: [DataType.Date, DataType.Time, DataType.DateTime],
            rows: [
                (new DateOnly(2024, 1, 15), new TimeOnly(9, 30, 0), new DateTime(2024, 1, 15, 9, 30, 0)),
                (new DateOnly(2025, 12, 31), new TimeOnly(23, 59, 59), new DateTime(2025, 12, 31, 23, 59, 59)),
                (new DateOnly(2000, 2, 29), new TimeOnly(0, 0, 1), new DateTime(2000, 2, 29, 0, 0, 1))
            ]);
    }

    [Test]
    [DisplayName("QVD null symbols возвращают DBNull")]
    public async Task Null_symbols_return_dbnull()
    {
        await using var rawReader = await OpenAsync("nulls.qvd");
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "optional_text", "optional_num"],
            types: [DataType.Integer, DataType.Text, DataType.Number],
            rows: [
                (1, "present", 10.5d),
                (2, DBNull.Value, 20d),
                (3, "also", DBNull.Value),
                (4, DBNull.Value, DBNull.Value),
                (5, "last", -1.25d)
            ]);
    }

    [Test]
    [DisplayName("QVD mixed dual values остаются Text")]
    public async Task Mixed_dual_values_stay_text()
    {
        await using var rawReader = await OpenAsync("mixed_dual_values.qvd");
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["row_id", "code"],
            types: [DataType.Integer, DataType.Text],
            rows: [
                (1, "001"),
                (2, "002"),
                (3, "A03"),
                (4, "003")
            ]);
    }

    [Test]
    [DisplayName("QVD high cardinality читает поток и работает с Limit")]
    public async Task High_cardinality_reads_stream_and_works_with_limit()
    {
        await using var rawReader = await OpenAsync("high_cardinality.qvd");
        await using var reader = rawReader.Normalize().Limit(5);

        await Assert.That(reader).HaveData(
            columns: ["id", "category"],
            types: [DataType.Integer, DataType.Text],
            rows: [
                (1, "beta"),
                (2, "gamma"),
                (3, "delta"),
                (4, "epsilon"),
                (5, "alpha")
            ]);
    }

    [Test]
    [DisplayName("QVD empty table возвращает схему без строк")]
    public async Task Empty_table_returns_schema_without_rows()
    {
        await using var rawReader = await OpenAsync("empty_table.qvd");
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "name", "amount"],
            types: [DataType.Text, DataType.Text, DataType.Text],
            rows: []);
    }

    [Test]
    [DisplayName("QVD special names сохраняет имена колонок case-sensitive")]
    public async Task Special_names_preserve_exact_column_names()
    {
        await using var rawReader = await OpenAsync("special_names.qvd");
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["Order ID", "Город", "amount.value", "Name", "name", "% Доля [спец]"],
            types: [DataType.Integer, DataType.Text, DataType.Number, DataType.Text, DataType.Text, DataType.Number],
            rows: [
                (101, "Москва", 15.5d, "Ivan", "ivan", 0.25d),
                (102, "Санкт-Петербург", 7d, "Olga", "olga", 0.75d)
            ]);
    }

    [Test]
    [DisplayName("QVD unsupported форматы читаются как текст пока нет отдельной политики")]
    public async Task Unsupported_formats_read_as_text_for_now()
    {
        await using var rawReader = await OpenAsync("binary_or_unsupported.qvd");
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "interval_value", "money_value", "weird_string"],
            types: [DataType.Integer, DataType.Text, DataType.Text, DataType.Text],
            rows: [
                (1, "1 12:00:00", "$1,234.56", "1920230146"),
                (2, "0 06:00:00", "$-9.99", "line1\u0001\u0002ctrl")
            ]);
    }

    [Test]
    [DisplayName("QVD corrupted layout кидает provider exception")]
    public async Task Corrupted_layout_throws_provider_exception()
    {
        await Assert.That(async () => await OpenAsync("corrupted_layout.qvd"))
            .ThrowsExactly<QvdFormatProviderException>()
            .WithMessage("Could not read QVD file 'corrupted_layout.qvd'. Unexpected QVD format or IO error.");
    }

    [Test]
    [DisplayName("QVD после Normalize работает с Where")]
    public async Task Normalized_reader_works_with_where()
    {
        await using var rawReader = await OpenAsync("special_names.qvd");
        await using var reader = rawReader
            .Normalize()
            .Where(row => row.Text("Город") == "Москва" && row.Integer("Order ID") == 101);

        await Assert.That(reader.Read()).IsTrue();
        await Assert.That(reader.GetValue(reader.GetOrdinal("Name"))).IsEqualTo("Ivan");
        await Assert.That(reader.Read()).IsFalse();
    }

    private static ValueTask<System.Data.Common.DbDataReader> OpenAsync(string fileName)
    {
        return Provider.OpenReaderAsync(
            Source,
            new QvdTableConfig
            {
                FileName = fileName
            });
    }

    private static string GetFixtureRoot()
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixtures", "Qvd");
    }
}

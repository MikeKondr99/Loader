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
    [DisplayName("QVD bit packing на границах байтов читает индексы символов")]
    public async Task Bit_packing_boundaries_read_symbol_indexes()
    {
        await using var rawReader = await OpenAsync("bit_packing_boundaries.qvd");
        await using var reader = rawReader.Normalize().Limit(5);

        await Assert.That(reader).HaveData(
            columns: ["w1", "w2", "w7", "w8", "w9", "w15", "w16", "w17"],
            types: [DataType.Integer, DataType.Integer, DataType.Integer, DataType.Integer, DataType.Integer, DataType.Integer, DataType.Integer, DataType.Integer],
            rows: [
                (0, 0, 0, 0, 0, 0, 0, 0),
                (1, 1, 1, 1, 1, 1, 1, 1),
                (0, 2, 2, 2, 2, 2, 2, 2),
                (1, 0, 3, 3, 3, 3, 3, 3),
                (0, 1, 4, 4, 4, 4, 4, 4)
            ]);
    }

    [Test]
    [DisplayName("QVD bias variants и отрицательный индекс возвращают DBNull")]
    public async Task Bias_variants_and_negative_index_return_dbnull()
    {
        await using var rawReader = await OpenAsync("bias_variants.qvd");
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["bias_0", "bias_minus2", "bias_minus1", "bias_plus1"],
            types: [DataType.Text, DataType.Text, DataType.Text, DataType.Text],
            rows: [
                ("aa", "aa", "aa", "bb"),
                ("bb", DBNull.Value, DBNull.Value, "cc"),
                ("cc", "cc", "cc", "bb")
            ]);
    }

    [Test]
    [DisplayName("QVD null через отрицательный индекс возвращает DBNull")]
    public async Task Null_negative_index_returns_dbnull()
    {
        await using var rawReader = await OpenAsync("null_negative_index.qvd");
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "val"],
            types: [DataType.Integer, DataType.Text],
            rows: [
                (1, "x"),
                (2, DBNull.Value),
                (3, "y")
            ]);
    }

    [Test]
    [DisplayName("QVD single value BitWidth 0 читает повторяющуюся константу")]
    public async Task Single_value_bitwidth_zero_reads_constant()
    {
        await using var rawReader = await OpenAsync("single_value_bitwidth0.qvd");
        await using var reader = rawReader.Normalize().Limit(5);

        await Assert.That(reader).HaveData(
            columns: ["id", "constant"],
            types: [DataType.Integer, DataType.Text],
            rows: [
                (0, "same"),
                (1, "same"),
                (2, "same"),
                (3, "same"),
                (4, "same")
            ]);
    }

    [Test]
    [DisplayName("QVD empty BitWidth 0 возвращает схему без строк")]
    public async Task Empty_bitwidth_zero_returns_schema_without_rows()
    {
        await using var rawReader = await OpenAsync("empty_bitwidth0.qvd");
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "name", "amount"],
            types: [DataType.Text, DataType.Text, DataType.Text],
            rows: []);
    }

    [Test]
    [DisplayName("QVD duplicate column names падает при Normalize")]
    public async Task Duplicate_column_names_throw_on_normalize()
    {
        await using var rawReader = await OpenAsync("duplicate_column_names.qvd");

        await Assert.That(() => rawReader.Normalize())
            .ThrowsExactly<DuplicateDataFieldNameException>()
            .WithMessage("Column name 'value' is duplicated.");
    }

    [Test]
    [DisplayName("QVD case-sensitive имена Name и name доступны отдельно")]
    public async Task Case_sensitive_names_are_distinct()
    {
        await using var rawReader = await OpenAsync("case_sensitive_names.qvd");
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["Name", "name"],
            types: [DataType.Text, DataType.Text],
            rows: [
                ("UPPER_1", "lower_2"),
                ("UPPER_2", "lower_1")
            ]);
    }

    [Test]
    [DisplayName("QVD many columns сохраняет schema и ordinal lookup")]
    public async Task Many_columns_preserve_schema_and_ordinals()
    {
        await using var rawReader = await OpenAsync("many_columns.qvd");
        await using var reader = rawReader.Normalize();

        await Assert.That(reader.FieldCount).IsEqualTo(120);
        await Assert.That(reader.GetName(0)).IsEqualTo("c001");
        await Assert.That(reader.GetName(119)).IsEqualTo("c120");
        await Assert.That(reader.GetOrdinal("c001")).IsEqualTo(0);
        await Assert.That(reader.GetOrdinal("c120")).IsEqualTo(119);

        await Assert.That(reader.Read()).IsTrue();
        await Assert.That(reader.GetValue(0)).IsEqualTo(10);
        await Assert.That(reader.GetValue(119)).IsEqualTo(1200);
    }

    [Test]
    [DisplayName("QVD long string symbol читает длинное значение целиком")]
    public async Task Long_string_symbol_reads_full_value()
    {
        await using var rawReader = await OpenAsync("long_string_symbol.qvd");
        await using var reader = rawReader.Normalize();

        await Assert.That(reader.Read()).IsTrue();
        await Assert.That(reader.GetValue(1)).IsEqualTo("small");
        await Assert.That(reader.Read()).IsTrue();

        var value = (string)reader.GetValue(1);
        await Assert.That(value.StartsWith("Строка-гигант_", StringComparison.Ordinal)).IsTrue();
        await Assert.That(value.Length).IsGreaterThan(100_000);
    }

    [Test]
    [DisplayName("QVD unicode имена и значения сохраняются")]
    public async Task Unicode_names_and_values_are_preserved()
    {
        await using var rawReader = await OpenAsync("unicode_names_values.qvd");
        await using var reader = rawReader.Normalize();

        await Assert.That(reader.GetName(0)).IsEqualTo("Город_поля");
        await Assert.That(reader.GetName(1)).IsEqualTo("emoji_🚀");
        await Assert.That(reader.GetName(2)).IsEqualTo("שדה_עברית");

        await Assert.That(reader.Read()).IsTrue();
        await Assert.That(reader.GetValue(0)).IsEqualTo("Ёжик в тумане");
        await Assert.That(reader.GetValue(1)).IsEqualTo("🎉✨");
        await Assert.That(reader.GetValue(2)).IsEqualTo("שָׁלוֹם עוֹלָם");

        await Assert.That(reader.Read()).IsTrue();
        await Assert.That(reader.GetValue(2)).IsEqualTo("مرحبا بالعالم");
        await Assert.That(reader.GetValue(3)).IsEqualTo("tab\tnewline\nbell\u0007");
    }

    [Test]
    [DisplayName("QVD date time boundary values читаются без смещения")]
    public async Task Temporal_boundary_values_are_read_without_shift()
    {
        await using var epochRaw = await OpenAsync("date_epoch_1899_12_30.qvd");
        await using var epoch = epochRaw.Normalize();
        await Assert.That(epoch.Read()).IsTrue();
        await Assert.That(epoch.GetValue(1)).IsEqualTo(new DateOnly(1899, 12, 30));
        await Assert.That(epoch.Read()).IsTrue();
        await Assert.That(epoch.GetValue(1)).IsEqualTo(new DateOnly(1899, 12, 29));

        await using var leapRaw = await OpenAsync("date_leap_day.qvd");
        await using var leap = leapRaw.Normalize();
        await Assert.That(leap.Read()).IsTrue();
        await Assert.That(leap.GetValue(1)).IsEqualTo(new DateOnly(2024, 2, 29));
        await Assert.That(leap.Read()).IsTrue();
        await Assert.That(leap.GetValue(1)).IsEqualTo(new DateOnly(2000, 2, 29));
    }

    [Test]
    [DisplayName("QVD time boundary values читаются без потери секунды")]
    public async Task Time_boundary_values_are_read_without_second_loss()
    {
        await using var midnightRaw = await OpenAsync("time_midnight.qvd");
        await using var midnight = midnightRaw.Normalize();
        await Assert.That(midnight.Read()).IsTrue();
        await Assert.That(midnight.GetValue(1)).IsEqualTo(new TimeOnly(0, 0, 0));
        await Assert.That(midnight.Read()).IsTrue();
        await Assert.That(midnight.GetValue(1)).IsEqualTo(new TimeOnly(12, 0, 0));

        await using var endRaw = await OpenAsync("time_235959.qvd");
        await using var end = endRaw.Normalize();
        await Assert.That(end.Read()).IsTrue();
        await Assert.That(end.GetValue(1)).IsEqualTo(new TimeOnly(23, 59, 59));
        await Assert.That(end.Read()).IsTrue();
        await Assert.That(end.GetValue(1)).IsEqualTo(new TimeOnly(0, 0, 1));
    }

    [Test]
    [DisplayName("QVD dual temporal mismatch доверяет display string")]
    public async Task Dual_temporal_mismatch_prefers_display_string()
    {
        await using var rawReader = await OpenAsync("dual_temporal_mismatch.qvd");
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "date_value"],
            types: [DataType.Integer, DataType.Date],
            rows: [
                (1, new DateOnly(2030, 1, 1)),
                (2, new DateOnly(1985, 5, 5))
            ]);
    }

    [Test]
    [DisplayName("QVD dual non-temporal игнорирует numeric prefix и читает display string")]
    public async Task Dual_non_temporal_uses_display_string()
    {
        await using var rawReader = await OpenAsync("dual_non_temporal.qvd");
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["dual_int", "dual_dbl"],
            types: [DataType.Text, DataType.Text],
            rows: [
                ("forty-two", "полтора"),
                ("минус семь", "почти сто")
            ]);
    }

    [Test]
    [DisplayName("QVD row index out of range падает при чтении строки")]
    public async Task Row_index_out_of_range_throws_on_read()
    {
        await using var reader = await OpenAsync("row_index_out_of_range.qvd");

        await Assert.That(reader.Read()).IsTrue();
        await Assert.That(() => reader.Read())
            .ThrowsExactly<QvdFormatProviderException>()
            .WithMessage("Could not read QVD file 'row_index_out_of_range.qvd'. Could not read QVD row section.");
    }

    [Test]
    [DisplayName("QVD empty symbol table with rows падает при чтении строки")]
    public async Task Empty_symbol_table_with_rows_throws_on_read()
    {
        await using var reader = await OpenAsync("empty_symbols_with_rows.qvd");

        await Assert.That(() => reader.Read())
            .ThrowsExactly<QvdFormatProviderException>()
            .WithMessage("Could not read QVD file 'empty_symbols_with_rows.qvd'. Could not read QVD row section.");
    }

    [Test]
    [DisplayName("QVD corrupted provider files кидают format exception на open")]
    [Arguments("corrupted_binary_layout.qvd")]
    [Arguments("corrupted_xml_header.qvd")]
    [Arguments("truncated_double_symbol.qvd")]
    [Arguments("truncated_int_symbol.qvd")]
    [Arguments("truncated_rows.qvd")]
    [Arguments("truncated_string_symbol.qvd")]
    public async Task Corrupted_provider_files_throw_on_open(string fileName)
    {
        await Assert.That(async () => await OpenAsync(fileName))
            .ThrowsExactly<QvdFormatProviderException>();
    }

    [Test]
    [DisplayName("QVD missing file кидает file open provider exception")]
    public async Task Missing_file_throws_file_open_provider_exception()
    {
        await Assert.That(async () => await OpenAsync("missing.qvd"))
            .ThrowsExactly<QvdFileOpenProviderException>()
            .WithMessage("Could not open QVD file 'missing.qvd'.");
    }

    [Test]
    [DisplayName("QVD non-seekable source кидает provider exception")]
    public async Task Non_seekable_source_throws_provider_exception()
    {
        var source = new NonSeekableQvdSource(Path.Combine(GetFixtureRoot(), "stream_source_valid.qvd"));

        await Assert.That(async () => await Provider.OpenReaderAsync(
                source,
                new QvdTableConfig
                {
                    FileName = "stream_source_valid.qvd"
                }))
            .ThrowsExactly<QvdFormatProviderException>()
            .WithMessage("Could not read QVD file 'stream_source_valid.qvd'. QVD reader requires a seekable stream because symbol tables and rows are stored in different file sections.");
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

    private sealed class NonSeekableQvdSource : IFileSource
    {
        private readonly string _path;

        public NonSeekableQvdSource(string path)
        {
            _path = path;
        }

        public Stream OpenRead(string fileName)
        {
            return new NonSeekableStream(File.ReadAllBytes(_path));
        }
    }

    private sealed class NonSeekableStream : MemoryStream
    {
        public NonSeekableStream(byte[] buffer)
            : base(buffer)
        {
        }

        public override bool CanSeek => false;
    }
}

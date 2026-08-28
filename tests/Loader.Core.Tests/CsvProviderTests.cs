using System.Text;
using Loader.Core.Providers.Csv;
using Loader.Core.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Loader.Core.Tests;

public sealed class CsvProviderTests
{
    private static readonly CsvProvider Provider = new();

    [Test]
    [DisplayName("Csv с числами и датами выдает типы Text и значения строками")]
    public async Task Reads_inline_csv_values_as_strings()
    {
        var source = new InlineCsv(
            """
            id,name,amount,active,created
            1,Moscow,10.50,true,2026-01-02
            2,London,20.00,false,2026-02-03
            """);

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new CsvTableConfig
            {
                FileName = "any-file-name.csv"
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "name", "amount", "active", "created"],
            types: [DataType.Text, DataType.Text, DataType.Text, DataType.Text, DataType.Text],
            rows: [
                ("1", "Moscow", "10.50", "true", "2026-01-02"),
                ("2", "London", "20.00", "false", "2026-02-03")
            ]);
    }

    [Test]
    [DisplayName("Csv с разделителем ; и quoted values выдает строки без внешних кавычек")]
    public async Task Reads_custom_delimiter_and_quoted_values()
    {
        var source = new InlineCsv(
            """
            id;name;note
            1;Moscow;"hello;world"
            2;London;" spaced value "
            """);

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new CsvTableConfig
            {
                FileName = "any-file-name.csv",
                Delimiter = ';'
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "name", "note"],
            types: [DataType.Text, DataType.Text, DataType.Text],
            rows: [
                ("1", "Moscow", "hello;world"),
                ("2", "London", " spaced value ")
            ]);
    }

    [Test]
    [DisplayName("Csv \"Moscow\" пробел после кавычки сохраняет как часть значения")]
    public async Task Whitespace_after_closing_quote_is_kept_in_value()
    {
        var source = new InlineCsv(
            "id,name,note\r\n1,\"Moscow\" ,\"quoted value\" ");

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new CsvTableConfig
            {
                FileName = "any-file-name.csv"
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "name", "note"],
            types: [DataType.Text, DataType.Text, DataType.Text],
            rows: [
                ("1", "Moscow ", "quoted value ")
            ]);
    }

    [Test]
    [DisplayName("Csv валидные quoted значения читаются без внешних кавычек")]
    public async Task Valid_quoted_values_are_read_without_outer_quotes()
    {
        var source = new InlineCsv(
            "id,note\r\n1,\" spaced value \"\r\n2,\"say \"\"hello\"\"\"\r\n3,\"hello\r\nworld\"");

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new CsvTableConfig
            {
                FileName = "any-file-name.csv"
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "note"],
            types: [DataType.Text, DataType.Text],
            rows: [
                ("1", " spaced value "),
                ("2", "say \"hello\""),
                ("3", "hello\r\nworld")
            ]);
    }

    [Test]
    [DisplayName("Csv пробел перед кавычкой делает кавычки обычным текстом")]
    public async Task Whitespace_before_opening_quote_keeps_quotes_as_text()
    {
        var source = new InlineCsv(
            ", \"value\" ,");

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new CsvTableConfig
            {
                FileName = "any-file-name.csv",
                HasHeader = false
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["A", "B", "C"],
            types: [DataType.Text, DataType.Text, DataType.Text],
            rows: [
                ("", " \"value\" ", "")
            ]);
    }

    [Test]
    [DisplayName("Csv текст после закрывающей кавычки добавляет в значение")]
    public async Task Text_after_closing_quote_is_appended_to_value()
    {
        var source = new InlineCsv(
            "value\r\n\"abc\"tail");

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new CsvTableConfig
            {
                FileName = "any-file-name.csv"
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["value"],
            types: [DataType.Text],
            rows: [
                ValueTuple.Create("abctail")
            ]);
    }

    [Test]
    [DisplayName("Csv escaped quote и текст после кавычки остаются в одном значении")]
    public async Task Escaped_quote_and_text_after_closing_quote_are_read_as_one_value()
    {
        var source = new InlineCsv(
            "value\r\n\"a\"\"b\" tail");

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new CsvTableConfig
            {
                FileName = "any-file-name.csv"
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["value"],
            types: [DataType.Text],
            rows: [
                ValueTuple.Create("a\"b tail")
            ]);
    }

    [Test]
    [DisplayName("Csv с UTF-8 BOM не добавляет BOM в имя первой колонки")]
    public async Task Utf8_bom_is_not_part_of_first_header_name()
    {
        var source = new InlineCsv("\uFEFFid,name\r\n1,Moscow");

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new CsvTableConfig
            {
                FileName = "any-file-name.csv"
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "name"],
            types: [DataType.Text, DataType.Text],
            rows: [
                ("1", "Moscow")
            ]);
    }

    [Test]
    [DisplayName("Csv в UTF-8 читает Unicode значения как строки")]
    public async Task Utf8_reads_unicode_values()
    {
        var source = new InlineCsv("id,city,note\r\n1,Москва,Привет мир");

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new CsvTableConfig
            {
                FileName = "any-file-name.csv"
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "city", "note"],
            types: [DataType.Text, DataType.Text, DataType.Text],
            rows: [
                ("1", "Москва", "Привет мир")
            ]);
    }

    [Test]
    [DisplayName("Csv в UTF-16 LE с явной Encoding читает Unicode значения как строки")]
    public async Task Utf16_little_endian_reads_unicode_values_when_encoding_is_configured()
    {
        var source = new InlineCsv(
            "id,city,note\r\n1,Москва,Привет мир",
            Encoding.Unicode);

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new CsvTableConfig
            {
                FileName = "any-file-name.csv",
                Encoding = Encoding.Unicode
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "city", "note"],
            types: [DataType.Text, DataType.Text, DataType.Text],
            rows: [
                ("1", "Москва", "Привет мир")
            ]);
    }

    [Test]
    [DisplayName("Csv в UTF-16 BE с явной Encoding читает Unicode значения как строки")]
    public async Task Utf16_big_endian_reads_unicode_values_when_encoding_is_configured()
    {
        var source = new InlineCsv(
            "id,city,note\r\n1,Москва,Привет мир",
            Encoding.BigEndianUnicode);

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new CsvTableConfig
            {
                FileName = "any-file-name.csv",
                Encoding = Encoding.BigEndianUnicode
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "city", "note"],
            types: [DataType.Text, DataType.Text, DataType.Text],
            rows: [
                ("1", "Москва", "Привет мир")
            ]);
    }

    [Test]
    [DisplayName("Csv с CRLF line endings читает строки как обычные записи")]
    public async Task CrLf_line_endings_read_rows()
    {
        var source = new InlineCsv("id,name\r\n1,Moscow\r\n2,London\r\n");

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new CsvTableConfig
            {
                FileName = "any-file-name.csv"
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "name"],
            types: [DataType.Text, DataType.Text],
            rows: [
                ("1", "Moscow"),
                ("2", "London")
            ]);
    }

    [Test]
    [DisplayName("Csv с последней строкой без newline читает последнюю запись")]
    public async Task Last_row_without_newline_is_read()
    {
        var source = new InlineCsv("id,name\r\n1,Moscow\r\n2,London");

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new CsvTableConfig
            {
                FileName = "any-file-name.csv"
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "name"],
            types: [DataType.Text, DataType.Text],
            rows: [
                ("1", "Moscow"),
                ("2", "London")
            ]);
    }

    [Test]
    [DisplayName("Csv с delimiter внутри quoted value не делит значение на колонки")]
    public async Task Delimiter_inside_quoted_value_is_part_of_value()
    {
        var source = new InlineCsv("id,note\r\n1,\"hello,world\"");

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new CsvTableConfig
            {
                FileName = "any-file-name.csv"
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "note"],
            types: [DataType.Text, DataType.Text],
            rows: [
                ("1", "hello,world")
            ]);
    }

    [Test]
    [DisplayName("Csv с повторяющимися header names кидает DuplicateDataFieldNameException")]
    public async Task Duplicate_headers_throw_schema_exception()
    {
        var source = new InlineCsv(
            """
            id,id
            1,2
            """);

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new CsvTableConfig
            {
                FileName = "any-file-name.csv"
            });

        await Assert.That(() => rawReader.Normalize())
            .ThrowsExactly<DuplicateDataFieldNameException>()
            .WithMessage("Column name 'id' is duplicated.");
    }

    [Test]
    [DisplayName("Csv с пустым файлом и header=true кидает NoHeaderCsvProviderException")]
    public async Task Empty_file_with_header_required_throws_provider_exception()
    {
        var source = new InlineCsv("");

        await Assert.That(async () => await Provider.OpenReaderAsync(
                source,
                new CsvTableConfig
                {
                    FileName = "any-file-name.csv"
                }))
            .ThrowsExactly<NoHeaderCsvProviderException>()
            .WithMessage("CSV file 'any-file-name.csv' does not contain a header row.");
    }

    [Test]
    [DisplayName("Csv только с header выдает схему и без строк")]
    public async Task Header_only_returns_schema_and_no_rows()
    {
        var source = new InlineCsv("id,name,amount");

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new CsvTableConfig
            {
                FileName = "any-file-name.csv"
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "name", "amount"],
            types: [DataType.Text, DataType.Text, DataType.Text],
            rows: []);
    }

    [Test]
    [DisplayName("Csv с одной пустой ячейкой выдает пустую строку")]
    public async Task Single_empty_field_returns_empty_string()
    {
        var source = new InlineCsv(
            """
            value
            ""
            """);

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new CsvTableConfig
            {
                FileName = "any-file-name.csv"
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["value"],
            types: [DataType.Text],
            rows: [
                ValueTuple.Create("")
            ]);
    }

    [Test]
    [DisplayName("Csv с escaped quote внутри quoted value выдает кавычку в строке")]
    public async Task Escaped_quote_inside_quoted_value_returns_quote_character()
    {
        var source = new InlineCsv("value\r\n\"say \"\"hello\"\"\"");

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new CsvTableConfig
            {
                FileName = "any-file-name.csv"
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["value"],
            types: [DataType.Text],
            rows: [
                ValueTuple.Create("say \"hello\"")
            ]);
    }

    [Test]
    [DisplayName("Csv с backslash quote выдает значение без специального escape")]
    public async Task Backslash_quote_is_not_csv_escape_by_default()
    {
        var source = new InlineCsv(
            """
            value
            \"text\"
            """);

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new CsvTableConfig
            {
                FileName = "any-file-name.csv"
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["value"],
            types: [DataType.Text],
            rows: [
                ValueTuple.Create("\\\"text\\\"")
            ]);
    }

    [Test]
    [DisplayName("Csv с quoted newline внутри значения выдает одну строку с переносом")]
    public async Task Quoted_newline_inside_value_returns_single_row()
    {
        var source = new InlineCsv(
            "value\r\n\"hello\r\nworld\"");

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new CsvTableConfig
            {
                FileName = "any-file-name.csv"
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["value"],
            types: [DataType.Text],
            rows: [
                ValueTuple.Create("hello\r\nworld")
            ]);
    }

    [Test]
    [DisplayName("Csv без header выдает имена колонок A B и строки как Text")]
    public async Task No_header_uses_generated_column_names_and_text_values()
    {
        var source = new InlineCsv("1,Moscow\r\n2,London");

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new CsvTableConfig
            {
                FileName = "any-file-name.csv",
                HasHeader = false
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["A", "B"],
            types: [DataType.Text, DataType.Text],
            rows: [
                ("1", "Moscow"),
                ("2", "London")
            ]);
    }

    [Test]
    [DisplayName("Csv без header генерирует имена колонок после Z как AA AB")]
    public async Task No_header_generates_excel_column_names_after_z()
    {
        var source = new InlineCsv(string.Join(",", Enumerable.Range(1, 28)));

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new CsvTableConfig
            {
                FileName = "any-file-name.csv",
                HasHeader = false
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: [
                "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N",
                "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z", "AA", "AB"
            ],
            types: Enumerable.Repeat(DataType.Text, 28).ToArray(),
            rows: [
                (
                    "1", "2", "3", "4", "5", "6", "7", "8",
                    "9", "10", "11", "12", "13", "14", "15", "16",
                    "17", "18", "19", "20", "21", "22", "23", "24",
                    "25", "26", "27", "28")
            ]);
    }

    [Test]
    [DisplayName("Csv без header и короткой строкой выдает DBNull в отсутствующей ячейке")]
    public async Task No_header_short_row_returns_db_null_for_missing_value()
    {
        var source = new InlineCsv("1,2,3\r\n4,5");

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new CsvTableConfig
            {
                FileName = "any-file-name.csv",
                HasHeader = false
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["A", "B", "C"],
            types: [DataType.Text, DataType.Text, DataType.Text],
            rows: [
                ("1", "2", "3"),
                ("4", "5", DBNull.Value)
            ]);
    }

    [Test]
    [DisplayName("Csv без header и длинной строкой игнорирует значения сверх первой строки")]
    public async Task No_header_long_row_ignores_extra_values()
    {
        var source = new InlineCsv("1,2\r\n3,4,5");

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new CsvTableConfig
            {
                FileName = "any-file-name.csv",
                HasHeader = false
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["A", "B"],
            types: [DataType.Text, DataType.Text],
            rows: [
                ("1", "2"),
                ("3", "4")
            ]);
    }

    [Test]
    [DisplayName("Csv с меньшим количеством значений чем header выдает DBNull в отсутствующей ячейке")]
    public async Task Short_row_returns_db_null_for_missing_value()
    {
        var source = new InlineCsv(
            """
            id,name,amount
            1,Moscow
            """);

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new CsvTableConfig
            {
                FileName = "any-file-name.csv"
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "name", "amount"],
            types: [DataType.Text, DataType.Text, DataType.Text],
            rows: [
                ("1", "Moscow", DBNull.Value)
            ]);
    }

    [Test]
    [DisplayName("Csv с большим количеством значений чем header игнорирует лишние значения")]
    public async Task Long_row_ignores_extra_values()
    {
        var source = new InlineCsv(
            """
            id,name
            1,Moscow,extra
            """);

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new CsvTableConfig
            {
                FileName = "any-file-name.csv"
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "name"],
            types: [DataType.Text, DataType.Text],
            rows: [
                ("1", "Moscow")
            ]);
    }

    [Test]
    [DisplayName("Csv незакрытая кавычка читает текст до конца поля")]
    public async Task Unclosed_quote_reads_text_until_field_end()
    {
        var source = new InlineCsv(
            """
            value
            "abc
            """);

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new CsvTableConfig
            {
                FileName = "any-file-name.csv"
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["value"],
            types: [DataType.Text],
            rows: [
                ValueTuple.Create("abc")
            ]);
    }

    [Test]
    [DisplayName("Csv с LF line endings читает строки как обычные записи")]
    public async Task Lf_line_endings_read_rows()
    {
        var source = new InlineCsv("id,name\n1,Alice\n2,Bob");

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new CsvTableConfig
            {
                FileName = "any-file-name.csv"
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "name"],
            types: [DataType.Text, DataType.Text],
            rows: [
                ("1", "Alice"),
                ("2", "Bob")
            ]);
    }

    [Test]
    [DisplayName("Csv unquoted значение сохраняет пробелы вокруг текста")]
    public async Task Unquoted_value_preserves_outer_whitespace()
    {
        var source = new InlineCsv("value\r\n text ");

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new CsvTableConfig
            {
                FileName = "any-file-name.csv"
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["value"],
            types: [DataType.Text],
            rows: [
                ValueTuple.Create(" text ")
            ]);
    }

    [Test]
    [DisplayName("Csv quoted empty string выдает пустую строку")]
    public async Task Quoted_empty_string_returns_empty_string()
    {
        var source = new InlineCsv("value\r\n\"\"");

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new CsvTableConfig
            {
                FileName = "any-file-name.csv"
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["value"],
            types: [DataType.Text],
            rows: [
                ValueTuple.Create("")
            ]);
    }

    [Test]
    [DisplayName("Csv строка из delimiters выдает пустые строки по всем колонкам")]
    public async Task Delimiters_only_row_returns_empty_strings()
    {
        var source = new InlineCsv(",,");

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new CsvTableConfig
            {
                FileName = "any-file-name.csv",
                HasHeader = false
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["A", "B", "C"],
            types: [DataType.Text, DataType.Text, DataType.Text],
            rows: [
                ("", "", "")
            ]);
    }

    [Test]
    [DisplayName("Csv header с пустым именем колонки сохраняет пустое имя")]
    public async Task Header_empty_column_name_is_kept()
    {
        var source = new InlineCsv("id,,name\r\n1,2,Alice");

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new CsvTableConfig
            {
                FileName = "any-file-name.csv"
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "", "name"],
            types: [DataType.Text, DataType.Text, DataType.Text],
            rows: [
                ("1", "2", "Alice")
            ]);
    }

    [Test]
    [DisplayName("Csv header сохраняет пробелы вокруг имени колонки")]
    public async Task Header_column_name_preserves_outer_whitespace()
    {
        var source = new InlineCsv(" id , name \r\n1,Alice");

        await using var rawReader = await Provider.OpenReaderAsync(
            source,
            new CsvTableConfig
            {
                FileName = "any-file-name.csv"
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: [" id ", " name "],
            types: [DataType.Text, DataType.Text],
            rows: [
                ("1", "Alice")
            ]);
    }
}

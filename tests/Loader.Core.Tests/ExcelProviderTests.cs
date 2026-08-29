using System.Data;
using System.IO.Compression;
using Loader.Core.Providers.Excel;
using Loader.Core.Sources;
using Loader.Core.Tests.Infrastructure;
using Sylvan.Data.Excel;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Loader.Core.Tests;

public sealed class ExcelProviderTests
{
    private static readonly ExcelProvider Provider = new();

    [Test]
    [DisplayName("Excel с header читает .xlsx и возвращает строковые значения")]
    public async Task Reads_xlsx_with_header_as_text_values()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var fileName = "basic-types.xlsx";
            await WriteWorkbookAsync(
                tempDirectory,
                fileName,
                ("basic_types", CreateTable(
                    columns: [("id", typeof(int)), ("city", typeof(string)), ("amount", typeof(decimal))],
                    rows: [
                        [1, "Moscow", 10.50m],
                        [2, "London", 20.00m]
                    ])));

            var source = new FileSystemSource(tempDirectory);

            var rawReader = await Provider.OpenReaderAsync(
                source,
                new ExcelTableConfig
                {
                    FileName = fileName,
                    WorksheetName = "basic_types",
                    HasHeader = true
                });
            await using var reader = rawReader.Normalize();

            await Assert.That(reader).HaveData(
                columns: ["id", "city", "amount"],
                types: [DataType.Text, DataType.Text, DataType.Text],
                rows: [
                    ("1", "Moscow", "10.5"),
                    ("2", "London", "20")
                ]);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    [DisplayName("Excel читает .xlsb workbook")]
    public async Task Reads_xlsb_workbook()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var fileName = "binary-workbook.xlsb";
            await WriteWorkbookAsync(
                tempDirectory,
                fileName,
                ("binary", CreateTable(
                    columns: [("id", typeof(int)), ("city", typeof(string))],
                    rows: [
                        [1, "Moscow"]
                    ])));

            var source = new FileSystemSource(tempDirectory);

            var rawReader = await Provider.OpenReaderAsync(
                source,
                new ExcelTableConfig
                {
                    FileName = fileName,
                    WorksheetName = "binary",
                    HasHeader = true
                });
            await using var reader = rawReader.Normalize();

            await Assert.That(reader).HaveData(
                columns: ["id", "city"],
                types: [DataType.Text, DataType.Text],
                rows: [
                    ("1", "Moscow")
                ]);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    [DisplayName("Excel читает указанный sheet по имени")]
    public async Task Reads_selected_worksheet_by_name()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var fileName = "selected-sheet.xlsx";
            await WriteWorkbookAsync(
                tempDirectory,
                fileName,
                ("first", CreateTable(
                    columns: [("id", typeof(int)), ("city", typeof(string))],
                    rows: [
                        [1, "Moscow"]
                    ])),
                ("sheet with spaces", CreateTable(
                    columns: [("id", typeof(int)), ("city", typeof(string))],
                    rows: [
                        [2, "London"]
                    ])));

            var source = new FileSystemSource(tempDirectory);

            var rawReader = await Provider.OpenReaderAsync(
                source,
                new ExcelTableConfig
                {
                    FileName = fileName,
                    WorksheetName = "sheet with spaces",
                    HasHeader = true
                });
            await using var reader = rawReader.Normalize();

            await Assert.That(reader).HaveData(
                columns: ["id", "city"],
                types: [DataType.Text, DataType.Text],
                rows: [
                    ("2", "London")
                ]);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    [DisplayName("Excel без имени sheet читает первый лист workbook")]
    public async Task Reads_first_worksheet_when_name_was_not_configured()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var fileName = "first-sheet.xlsx";
            await WriteWorkbookAsync(
                tempDirectory,
                fileName,
                ("first", CreateTable(
                    columns: [("id", typeof(int)), ("city", typeof(string))],
                    rows: [
                        [1, "Moscow"]
                    ])),
                ("second", CreateTable(
                    columns: [("id", typeof(int)), ("city", typeof(string))],
                    rows: [
                        [2, "London"]
                    ])));

            var source = new FileSystemSource(tempDirectory);

            var rawReader = await Provider.OpenReaderAsync(
                source,
                new ExcelTableConfig
                {
                    FileName = fileName,
                    HasHeader = true
                });
            await using var reader = rawReader.Normalize();

            await Assert.That(reader).HaveData(
                columns: ["id", "city"],
                types: [DataType.Text, DataType.Text],
                rows: [
                    ("1", "Moscow")
                ]);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    [DisplayName("Excel с неизвестным sheet кидает понятную ошибку")]
    public async Task Throws_when_worksheet_was_not_found()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var fileName = "missing-sheet.xlsx";
            await WriteWorkbookAsync(
                tempDirectory,
                fileName,
                ("sheet1", CreateTable(
                    columns: [("id", typeof(int))],
                    rows: [
                        [1]
                    ])));

            var source = new FileSystemSource(tempDirectory);

            await Assert.That(async () => await Provider.OpenReaderAsync(
                    source,
                    new ExcelTableConfig
                    {
                        FileName = fileName,
                        WorksheetName = "Missing",
                        HasHeader = true
                    }))
                .ThrowsExactly<ExcelWorksheetNotFoundProviderException>()
                .WithMessage("Worksheet 'Missing' was not found in Excel file 'missing-sheet.xlsx'. Available worksheets: sheet1.");
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    [DisplayName("Excel с базовыми CLR значениями в текущем режиме возвращает всё текстом")]
    public async Task Basic_clr_values_are_read_as_text_in_default_schema()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var fileName = "basic-clr-values.xlsx";
            await WriteWorkbookAsync(
                tempDirectory,
                fileName,
                ("basic_clr_values", CreateTable(
                    columns:
                    [
                        ("text", typeof(string)),
                        ("integer", typeof(int)),
                        ("number", typeof(decimal)),
                        ("boolean", typeof(bool)),
                        ("datetime", typeof(DateTime))
                    ],
                    rows: [
                        ["Moscow", 42, 10.50m, true, new DateTime(2026, 1, 2, 3, 4, 5)]
                    ])));

            var source = new FileSystemSource(tempDirectory);

            var rawReader = await Provider.OpenReaderAsync(
                source,
                new ExcelTableConfig
                {
                    FileName = fileName,
                    WorksheetName = "basic_clr_values",
                    HasHeader = true
                });
            await using var reader = rawReader.Normalize();

            await Assert.That(reader).HaveData(
                columns: ["text", "integer", "number", "boolean", "datetime"],
                types: [DataType.Text, DataType.Text, DataType.Text, DataType.Text, DataType.Text],
                rows: [
                    ("Moscow", "42", "10.5", "True", "2026-01-02T03:04:05")
                ]);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    [DisplayName("Excel без header генерирует имена A B и читает первую строку как данные")]
    public async Task No_header_generates_excel_column_names()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var fileName = "no-header.xlsx";
            await WriteWorkbookAsync(
                tempDirectory,
                fileName,
                ("no_header", CreateTable(
                    columns: [("id", typeof(int)), ("city", typeof(string))],
                    rows: [
                        [1, "Moscow"]
                    ])));

            var source = new FileSystemSource(tempDirectory);

            var rawReader = await Provider.OpenReaderAsync(
                source,
                new ExcelTableConfig
                {
                    FileName = fileName,
                    WorksheetName = "no_header",
                    HasHeader = false
                });
            await using var reader = rawReader.Normalize();

            await Assert.That(reader).HaveData(
                columns: ["A", "B"],
                types: [DataType.Text, DataType.Text],
                rows: [
                    ("id", "city"),
                    ("1", "Moscow")
                ]);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    [DisplayName("Excel только с header возвращает схему и пустой поток")]
    public async Task Header_only_returns_schema_and_empty_rows()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var fileName = "header-only.xlsx";
            await WriteWorkbookAsync(
                tempDirectory,
                fileName,
                ("header_only", CreateTable(
                    columns: [("id", typeof(int)), ("city", typeof(string))],
                    rows: [])));

            var source = new FileSystemSource(tempDirectory);

            var rawReader = await Provider.OpenReaderAsync(
                source,
                new ExcelTableConfig
                {
                    FileName = fileName,
                    WorksheetName = "header_only",
                    HasHeader = true
                });
            await using var reader = rawReader.Normalize();

            await Assert.That(reader).HaveData(
                columns: ["id", "city"],
                types: [DataType.Text, DataType.Text],
                rows: []);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    [DisplayName("Excel с пустой ячейкой возвращает DBNull")]
    public async Task Empty_cell_returns_db_null()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var fileName = "empty-cell.xlsx";
            await WriteWorkbookAsync(
                tempDirectory,
                fileName,
                ("empty_cell", CreateTable(
                    columns: [("id", typeof(int)), ("city", typeof(string))],
                    rows: [
                        [1, DBNull.Value],
                        [2, "London"]
                    ])));

            var source = new FileSystemSource(tempDirectory);

            var rawReader = await Provider.OpenReaderAsync(
                source,
                new ExcelTableConfig
                {
                    FileName = fileName,
                    WorksheetName = "empty_cell",
                    HasHeader = true
                });
            await using var reader = rawReader.Normalize();

            await Assert.That(reader).HaveData(
                columns: ["id", "city"],
                types: [DataType.Text, DataType.Text],
                rows: [
                    ("1", DBNull.Value),
                    ("2", "London")
                ]);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    [DisplayName("Excel с Unicode emoji кириллицей и RTL текстом читает символы без потери")]
    public async Task Unicode_values_are_read_without_character_loss()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var fileName = "unicode.xlsx";
            await WriteWorkbookAsync(
                tempDirectory,
                fileName,
                ("unicode", CreateTable(
                    columns: [("kind", typeof(string)), ("value", typeof(string))],
                    rows: [
                        ["cyrillic", "Москва"],
                        ["emoji", "hello 😀"],
                        ["rtl", "שלום"]
                    ])));

            var source = new FileSystemSource(tempDirectory);

            var rawReader = await Provider.OpenReaderAsync(
                source,
                new ExcelTableConfig
                {
                    FileName = fileName,
                    WorksheetName = "unicode",
                    HasHeader = true
                });
            await using var reader = rawReader.Normalize();

            await Assert.That(reader).HaveData(
                columns: ["kind", "value"],
                types: [DataType.Text, DataType.Text],
                rows: [
                    ("cyrillic", "Москва"),
                    ("emoji", "hello 😀"),
                    ("rtl", "שלום")
                ]);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    [DisplayName("Excel с нулем отрицательными и дробными числами фиксирует строковый формат Sylvan")]
    public async Task Numeric_edge_values_are_read_as_sylvan_text()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var fileName = "numeric-edge-values.xlsx";
            await WriteWorkbookAsync(
                tempDirectory,
                fileName,
                ("numeric_edge_values", CreateTable(
                    columns:
                    [
                        ("zero", typeof(int)),
                        ("negative_integer", typeof(int)),
                        ("negative_number", typeof(decimal)),
                        ("number", typeof(decimal)),
                        ("double_value", typeof(double))
                    ],
                    rows: [
                        [0, -42, -10.50m, 12345.6789m, 0.123456789d]
                    ])));

            var source = new FileSystemSource(tempDirectory);

            var rawReader = await Provider.OpenReaderAsync(
                source,
                new ExcelTableConfig
                {
                    FileName = fileName,
                    WorksheetName = "numeric_edge_values",
                    HasHeader = true
                });
            await using var reader = rawReader.Normalize();

            await Assert.That(reader).HaveData(
                columns: ["zero", "negative_integer", "negative_number", "number", "double_value"],
                types: [DataType.Text, DataType.Text, DataType.Text, DataType.Text, DataType.Text],
                rows: [
                    ("0", "-42", "-10.5", "12345.6789", "0.123456789")
                ]);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    [DisplayName("Excel с очень большим числом фиксирует строковый формат Sylvan")]
    public async Task Very_large_number_is_read_as_sylvan_text()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var fileName = "very-large-number.xlsx";
            await WriteWorkbookAsync(
                tempDirectory,
                fileName,
                ("very_large_number", CreateTable(
                    columns: [("value", typeof(double))],
                    rows: [
                        [1.2345678901234568E+29d]
                    ])));

            var source = new FileSystemSource(tempDirectory);

            var rawReader = await Provider.OpenReaderAsync(
                source,
                new ExcelTableConfig
                {
                    FileName = fileName,
                    WorksheetName = "very_large_number",
                    HasHeader = true
                });
            await using var reader = rawReader.Normalize();

            await Assert.That(reader).HaveData(
                columns: ["value"],
                types: [DataType.Text],
                rows: [
                    ValueTuple.Create("1.2345678901234568E+29")
                ]);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    [DisplayName("Excel с текстом начинающимся с равно читает значение как текст а не формулу")]
    public async Task Text_starting_with_equal_sign_is_read_as_text()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var fileName = "equals-text.xlsx";
            await WriteWorkbookAsync(
                tempDirectory,
                fileName,
                ("equals_text", CreateTable(
                    columns: [("id", typeof(int)), ("value", typeof(string))],
                    rows: [
                        [1, "=1+1"],
                        [2, "=SUM(A1:A2)"]
                    ])));

            var source = new FileSystemSource(tempDirectory);

            var rawReader = await Provider.OpenReaderAsync(
                source,
                new ExcelTableConfig
                {
                    FileName = fileName,
                    WorksheetName = "equals_text",
                    HasHeader = true
                });
            await using var reader = rawReader.Normalize();

            await Assert.That(reader).HaveData(
                columns: ["id", "value"],
                types: [DataType.Text, DataType.Text],
                rows: [
                    ("1", "=1+1"),
                    ("2", "=SUM(A1:A2)")
                ]);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    [DisplayName("Excel provider работает вместе с Where поверх Domain reader")]
    public async Task Supports_where_over_domain_reader()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var fileName = "where.xlsx";
            await WriteWorkbookAsync(
                tempDirectory,
                fileName,
                ("where", CreateTable(
                    columns: [("id", typeof(int)), ("city", typeof(string)), ("amount", typeof(decimal))],
                    rows: [
                        [1, "Moscow", 10.50m],
                        [2, "London", 20.00m],
                        [3, "Moscow", 30.25m]
                    ])));

            var source = new FileSystemSource(tempDirectory);

            var rawReader = await Provider.OpenReaderAsync(
                source,
                new ExcelTableConfig
                {
                    FileName = fileName,
                    WorksheetName = "where",
                    HasHeader = true
                });
            await using var reader = rawReader
                .Normalize()
                .Where(row => row.Text("city") == "Moscow" && row.Number("amount") > 20m);

            await Assert.That(reader).HaveData(
                columns: ["id", "city", "amount"],
                types: [DataType.Text, DataType.Text, DataType.Text],
                rows: [
                    ("3", "Moscow", "30.25")
                ]);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    [DisplayName("Excel с пустой строкой между данными пропускает пустую строку")]
    public async Task Empty_row_between_data_is_skipped()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var fileName = "empty-row.xlsx";
            await WriteWorkbookAsync(
                tempDirectory,
                fileName,
                ("empty_row", CreateTable(
                    columns: [("id", typeof(int)), ("city", typeof(string))],
                    rows: [
                        [1, "Moscow"],
                        [DBNull.Value, DBNull.Value],
                        [2, "London"]
                    ])));

            var source = new FileSystemSource(tempDirectory);

            var rawReader = await Provider.OpenReaderAsync(
                source,
                new ExcelTableConfig
                {
                    FileName = fileName,
                    WorksheetName = "empty_row",
                    HasHeader = true
                });
            await using var reader = rawReader.Normalize();

            await Assert.That(reader).HaveData(
                columns: ["id", "city"],
                types: [DataType.Text, DataType.Text],
                rows: [
                    ("1", "Moscow"),
                    ("2", "London")
                ]);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    [DisplayName("Excel с unsupported extension кидает ошибку определения типа workbook")]
    public async Task Unsupported_extension_throws_workbook_type_error()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var fileName = "unsupported.txt";
            await File.WriteAllTextAsync(Path.Combine(tempDirectory, fileName), "not excel");

            var source = new FileSystemSource(tempDirectory);

            await Assert.That(async () => await Provider.OpenReaderAsync(
                    source,
                    new ExcelTableConfig
                    {
                        FileName = fileName,
                        HasHeader = true
                    }))
                .ThrowsExactly<ExcelWorkbookOpenProviderException>()
                .WithMessage("Excel file 'unsupported.txt' could not be opened as a supported workbook.");
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    [DisplayName("Excel с corrupted xlsx кидает ошибку чтения workbook")]
    public async Task Corrupted_xlsx_throws_workbook_read_error()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var fileName = "corrupted.xlsx";
            await File.WriteAllTextAsync(Path.Combine(tempDirectory, fileName), "not excel");

            var source = new FileSystemSource(tempDirectory);

            await Assert.That(async () => await Provider.OpenReaderAsync(
                    source,
                    new ExcelTableConfig
                    {
                        FileName = fileName,
                        HasHeader = true
                    }))
                .ThrowsExactly<ExcelWorkbookOpenProviderException>()
                .WithMessage("Excel file 'corrupted.xlsx' could not be opened as a supported workbook.");
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    [DisplayName("Excel с отсутствующим файлом кидает provider exception с понятным сообщением")]
    public async Task Missing_file_throws_provider_exception()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var source = new FileSystemSource(tempDirectory);

            await Assert.That(async () => await Provider.OpenReaderAsync(
                    source,
                    new ExcelTableConfig
                    {
                        FileName = "missing.xlsx",
                        HasHeader = true
                    }))
                .ThrowsExactly<ExcelWorkbookOpenProviderException>()
                .WithMessage("Excel file 'missing.xlsx' could not be opened as a supported workbook.");
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    [DisplayName("Excel с формулами читает сохраненные значения формул")]
    public async Task Formula_cells_are_read_as_saved_formula_results()
    {
        var source = CreateFixtureSource();

        var rawReader = await Provider.OpenReaderAsync(
            source,
            new ExcelTableConfig
            {
                FileName = "excel-provider-complex.xlsx",
                WorksheetName = "formulas",
                HasHeader = true
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "left", "right", "total"],
            types: [DataType.Text, DataType.Text, DataType.Text, DataType.Text],
            rows: [
                ("1", "10", "5", "15"),
                ("2", "20", "7", "27")
            ]);
    }

    [Test]
    [DisplayName("Excel с формулой на другой sheet читает сохраненный результат формулы")]
    public async Task Cross_sheet_formula_is_read_as_saved_formula_result()
    {
        var source = CreateFixtureSource();

        var rawReader = await Provider.OpenReaderAsync(
            source,
            new ExcelTableConfig
            {
                FileName = "excel-provider-complex.xlsx",
                WorksheetName = "formula_cross_sheet",
                HasHeader = true
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "result"],
            types: [DataType.Text, DataType.Text],
            rows: [
                ("1", "42")
            ]);
    }

    [Test]
    [DisplayName("Excel provider с формулами работает вместе с Where поверх Domain reader")]
    public async Task Supports_where_over_formula_values()
    {
        var source = CreateFixtureSource();

        var rawReader = await Provider.OpenReaderAsync(
            source,
            new ExcelTableConfig
            {
                FileName = "excel-provider-complex.xlsx",
                WorksheetName = "formulas",
                HasHeader = true
            });
        await using var reader = rawReader
            .Normalize()
            .Where(row => row.Number("total") > 20m);

        await Assert.That(reader).HaveData(
            columns: ["id", "left", "right", "total"],
            types: [DataType.Text, DataType.Text, DataType.Text, DataType.Text],
            rows: [
                ("2", "20", "7", "27")
            ]);
    }

    [Test]
    [DisplayName("Excel с ошибками формул в режиме Exception кидает ошибку чтения значения")]
    public async Task Formula_errors_throw_value_error_by_default()
    {
        var source = CreateFixtureSource();

        var rawReader = await Provider.OpenReaderAsync(
            source,
            new ExcelTableConfig
            {
                FileName = "excel-provider-complex.xlsx",
                WorksheetName = "formula_errors",
                HasHeader = true
            });
        await using var reader = rawReader.Normalize();
        await reader.ReadAsync();

        await Assert.That(() => reader.GetValue(1))
            .Throws<DataReaderValueException>();
    }

    [Test]
    [DisplayName("Excel с ошибками формул в режиме Null возвращает DBNull")]
    public async Task Formula_errors_can_be_read_as_db_null()
    {
        var source = CreateFixtureSource();

        var rawReader = await Provider.OpenReaderAsync(
            source,
            new ExcelTableConfig
            {
                FileName = "excel-provider-complex.xlsx",
                WorksheetName = "formula_errors",
                HasHeader = true,
                FormulaErrorHandling = ExcelFormulaErrorMode.Null
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "na_error", "value_error", "div0_error"],
            types: [DataType.Text, DataType.Text, DataType.Text, DataType.Text],
            rows: [
                ("1", DBNull.Value, DBNull.Value, DBNull.Value)
            ]);
    }

    [Test]
    [DisplayName("Excel с ошибками формул в режиме String возвращает текст ошибки")]
    public async Task Formula_errors_can_be_read_as_error_text()
    {
        var source = CreateFixtureSource();

        var rawReader = await Provider.OpenReaderAsync(
            source,
            new ExcelTableConfig
            {
                FileName = "excel-provider-complex.xlsx",
                WorksheetName = "formula_errors",
                HasHeader = true,
                FormulaErrorHandling = ExcelFormulaErrorMode.String
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "na_error", "value_error", "div0_error"],
            types: [DataType.Text, DataType.Text, DataType.Text, DataType.Text],
            rows: [
                ("1", "#N/A", "#VALUE!", "#DIV/0!")
            ]);
    }

    [Test]
    [DisplayName("Excel с merged cells возвращает значение в первой ячейке и DBNull в хвосте merge")]
    public async Task Merged_cells_return_value_in_first_cell_and_db_null_in_merged_tail()
    {
        var source = CreateFixtureSource();

        var rawReader = await Provider.OpenReaderAsync(
            source,
            new ExcelTableConfig
            {
                FileName = "excel-provider-complex.xlsx",
                WorksheetName = "merged_cells",
                HasHeader = true
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "group", "merged_tail", "note"],
            types: [DataType.Text, DataType.Text, DataType.Text, DataType.Text],
            rows: [
                ("1", "merged value", DBNull.Value, "after merge"),
                ("2", "normal", "tail", "plain")
            ]);
    }

    [Test]
    [DisplayName("Excel с hidden row по умолчанию читает скрытую строку")]
    public async Task Hidden_rows_are_read_by_default()
    {
        var source = CreateFixtureSource();

        var rawReader = await Provider.OpenReaderAsync(
            source,
            new ExcelTableConfig
            {
                FileName = "excel-provider-complex.xlsx",
                WorksheetName = "hidden_rows",
                HasHeader = true
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "city"],
            types: [DataType.Text, DataType.Text],
            rows: [
                ("1", "Moscow"),
                ("2", "Hidden"),
                ("3", "London")
            ]);
    }

    [Test]
    [DisplayName("Excel с hidden row при ReadHiddenRows false пропускает скрытую строку")]
    public async Task Hidden_rows_can_be_skipped()
    {
        var source = CreateFixtureSource();

        var rawReader = await Provider.OpenReaderAsync(
            source,
            new ExcelTableConfig
            {
                FileName = "excel-provider-complex.xlsx",
                WorksheetName = "hidden_rows",
                HasHeader = true,
                ReadHiddenRows = false
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "city"],
            types: [DataType.Text, DataType.Text],
            rows: [
                ("1", "Moscow"),
                ("3", "London")
            ]);
    }

    [Test]
    [DisplayName("Excel с hidden sheet открывает скрытый лист если он явно указан по имени")]
    public async Task Hidden_worksheet_can_be_read_when_named_explicitly()
    {
        var source = CreateFixtureSource();

        var rawReader = await Provider.OpenReaderAsync(
            source,
            new ExcelTableConfig
            {
                FileName = "excel-provider-complex.xlsx",
                WorksheetName = "hidden_sheet",
                HasHeader = true
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "city"],
            types: [DataType.Text, DataType.Text],
            rows: [
                ("1", "Hidden Sheet")
            ]);
    }

    [Test]
    [DisplayName("Excel с hidden sheet при ReadHiddenWorksheets true читает скрытый лист")]
    public async Task Hidden_worksheet_can_be_read_when_enabled()
    {
        var source = CreateFixtureSource();

        var rawReader = await Provider.OpenReaderAsync(
            source,
            new ExcelTableConfig
            {
                FileName = "excel-provider-complex.xlsx",
                WorksheetName = "hidden_sheet",
                HasHeader = true,
                ReadHiddenWorksheets = true
            });
        await using var reader = rawReader.Normalize();

        await Assert.That(reader).HaveData(
            columns: ["id", "city"],
            types: [DataType.Text, DataType.Text],
            rows: [
                ("1", "Hidden Sheet")
            ]);
    }

    [Test]
    [DisplayName("Excel читает .xlsm workbook")]
    public async Task Reads_xlsm_workbook()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var fileName = "macro-enabled-workbook.xlsm";
            await WriteWorkbookAsync(
                tempDirectory,
                fileName,
                ("macro_enabled", CreateTable(
                    columns: [("id", typeof(int)), ("city", typeof(string))],
                    rows: [
                        [1, "Moscow"]
                    ])));

            var source = new FileSystemSource(tempDirectory);

            var rawReader = await Provider.OpenReaderAsync(
                source,
                new ExcelTableConfig
                {
                    FileName = fileName,
                    WorksheetName = "macro_enabled",
                    HasHeader = true
                });
            await using var reader = rawReader.Normalize();

            await Assert.That(reader).HaveData(
                columns: ["id", "city"],
                types: [DataType.Text, DataType.Text],
                rows: [
                    ("1", "Moscow")
                ]);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    [DisplayName("Excel range с header берет имена колонок из первой строки диапазона")]
    public async Task Range_with_header_reads_header_inside_range()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var fileName = "range-with-header.xlsx";
            await WriteWorkbookAsync(
                tempDirectory,
                fileName,
                ("range_header", CreateTable(
                    columns: [("ignored_a", typeof(string)), ("ignored_b", typeof(string)), ("ignored_c", typeof(string)), ("ignored_d", typeof(string))],
                    rows: [
                        ["skip", "id", "city", "skip"],
                        ["skip", "1", "Moscow", "skip"],
                        ["skip", "2", "London", "skip"],
                        ["skip", "3", "Berlin", "skip"]
                    ])));

            var source = new FileSystemSource(tempDirectory);

            var rawReader = await Provider.OpenReaderAsync(
                source,
                new ExcelTableConfig
                {
                    FileName = fileName,
                    WorksheetName = "range_header",
                    HasHeader = true,
                    Range = new ExcelCellRange
                    {
                        StartRow = 2,
                        StartColumn = 2,
                        EndRow = 4,
                        EndColumn = 3
                    }
                });
            await using var reader = rawReader.Normalize();

            await Assert.That(reader).HaveData(
                columns: ["id", "city"],
                types: [DataType.Text, DataType.Text],
                rows: [
                    ("1", "Moscow"),
                    ("2", "London")
                ]);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    [DisplayName("Excel range без header генерирует физические имена колонок B C")]
    public async Task Range_without_header_generates_physical_column_names()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var fileName = "range-without-header.xlsx";
            await WriteWorkbookAsync(
                tempDirectory,
                fileName,
                ("range_no_header", CreateTable(
                    columns: [("ignored_a", typeof(string)), ("ignored_b", typeof(string)), ("ignored_c", typeof(string)), ("ignored_d", typeof(string))],
                    rows: [
                        ["skip", "1", "Moscow", "skip"],
                        ["skip", "2", "London", "skip"],
                        ["skip", "3", "Berlin", "skip"]
                    ])));

            var source = new FileSystemSource(tempDirectory);

            var rawReader = await Provider.OpenReaderAsync(
                source,
                new ExcelTableConfig
                {
                    FileName = fileName,
                    WorksheetName = "range_no_header",
                    HasHeader = false,
                    Range = new ExcelCellRange
                    {
                        StartRow = 2,
                        StartColumn = 2,
                        EndRow = 3,
                        EndColumn = 3
                    }
                });
            await using var reader = rawReader.Normalize();

            await Assert.That(reader).HaveData(
                columns: ["B", "C"],
                types: [DataType.Text, DataType.Text],
                rows: [
                    ("1", "Moscow"),
                    ("2", "London")
                ]);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    [DisplayName("Excel range не читает строки после нижней границы")]
    public async Task Range_stops_at_end_row()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var fileName = "range-end-row.xlsx";
            await WriteWorkbookAsync(
                tempDirectory,
                fileName,
                ("range_end", CreateTable(
                    columns: [("id", typeof(string)), ("city", typeof(string))],
                    rows: [
                        ["1", "Moscow"],
                        ["2", "London"],
                        ["3", "Berlin"]
                    ])));

            var source = new FileSystemSource(tempDirectory);

            var rawReader = await Provider.OpenReaderAsync(
                source,
                new ExcelTableConfig
                {
                    FileName = fileName,
                    WorksheetName = "range_end",
                    HasHeader = true,
                    Range = new ExcelCellRange
                    {
                        StartRow = 1,
                        StartColumn = 1,
                        EndRow = 3,
                        EndColumn = 2
                    }
                });
            await using var reader = rawReader.Normalize();

            await Assert.That(reader).HaveData(
                columns: ["id", "city"],
                types: [DataType.Text, DataType.Text],
                rows: [
                    ("1", "Moscow"),
                    ("2", "London")
                ]);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    [DisplayName("Excel range возвращает DBNull для пустой ячейки внутри диапазона")]
    public async Task Range_returns_db_null_for_empty_cell_inside_range()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var fileName = "range-empty-cell.xlsx";
            await WriteWorkbookAsync(
                tempDirectory,
                fileName,
                ("range_empty", CreateTable(
                    columns: [("id", typeof(string)), ("city", typeof(string))],
                    rows: [
                        ["1", DBNull.Value],
                        ["2", "London"]
                    ])));

            var source = new FileSystemSource(tempDirectory);

            var rawReader = await Provider.OpenReaderAsync(
                source,
                new ExcelTableConfig
                {
                    FileName = fileName,
                    WorksheetName = "range_empty",
                    HasHeader = true,
                    Range = new ExcelCellRange
                    {
                        StartRow = 1,
                        StartColumn = 1,
                        EndRow = 3,
                        EndColumn = 2
                    }
                });
            await using var reader = rawReader.Normalize();

            await Assert.That(reader).HaveData(
                columns: ["id", "city"],
                types: [DataType.Text, DataType.Text],
                rows: [
                    ("1", DBNull.Value),
                    ("2", "London")
                ]);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    [DisplayName("Excel range parser принимает A1:B20 и отклоняет named range")]
    public async Task Range_parser_accepts_a1_notation_and_rejects_named_range()
    {
        await Assert.That(ExcelCellRange.TryParse("B2:D10", out var range)).IsTrue();
        await Assert.That(range!.StartRow).IsEqualTo(2);
        await Assert.That(range.StartColumn).IsEqualTo(2);
        await Assert.That(range.EndRow).IsEqualTo(10);
        await Assert.That(range.EndColumn).IsEqualTo(4);

        await Assert.That(ExcelCellRange.TryParse("B100:F", out var openEndRange)).IsTrue();
        await Assert.That(openEndRange!.StartRow).IsEqualTo(100);
        await Assert.That(openEndRange.StartColumn).IsEqualTo(2);
        await Assert.That(openEndRange.EndRow).IsNull();
        await Assert.That(openEndRange.EndColumn).IsEqualTo(6);

        await Assert.That(ExcelCellRange.TryParse("B:F", out var columnRange)).IsTrue();
        await Assert.That(columnRange!.StartRow).IsEqualTo(1);
        await Assert.That(columnRange.StartColumn).IsEqualTo(2);
        await Assert.That(columnRange.EndRow).IsNull();
        await Assert.That(columnRange.EndColumn).IsEqualTo(6);

        await Assert.That(ExcelCellRange.TryParse("SalesRange", out _)).IsFalse();
        await Assert.That(ExcelCellRange.TryParse("Sheet1!A1:B2", out _)).IsFalse();
        await Assert.That(ExcelCellRange.TryParse("B2:A1", out _)).IsFalse();
        await Assert.That(ExcelCellRange.TryParse("B100:F50", out _)).IsFalse();
    }

    [Test]
    [DisplayName("Excel range читает физический B4:H10, если первые строки листа пустые")]
    public async Task Range_uses_physical_excel_rows_when_first_rows_are_empty()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var fileName = "sparse-range.xlsx";
            await WriteSparseWorkbookAsync(tempDirectory, fileName);
            var source = new FileSystemSource(tempDirectory);

            var rawReader = await Provider.OpenReaderAsync(
                source,
                new ExcelTableConfig
                {
                    FileName = fileName,
                    WorksheetName = "Sales",
                    HasHeader = true,
                    Range = new ExcelCellRange
                    {
                        StartRow = 4,
                        StartColumn = 2,
                        EndRow = 10,
                        EndColumn = 8
                    }
                });
            await using var reader = rawReader.Normalize();

            await Assert.That(reader).HaveData(
                columns: ["Date", "Region", "Manager", "Product", "Quantity", "Price", "Revenue"],
                types: [DataType.Text, DataType.Text, DataType.Text, DataType.Text, DataType.Text, DataType.Text, DataType.Text],
                rows: [
                    ("2026-08-01", "North", "Anna", "Laptop", "4", "899", "3596"),
                    ("2026-08-03", "West", "Ilya", "Monitor", "7", "279.5", "1956.5"),
                    ("2026-08-06", "South", "Maria", "Keyboard", "12", "64.9", "778.8"),
                    ("2026-08-09", "East", "Oleg", "Mouse", "20", "29.9", "598"),
                    ("2026-08-13", "North", "Anna", "Dock", "5", "159", "795"),
                    ("2026-08-18", "West", "Ilya", "Laptop", "3", "949", "2847")
                ]);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    [DisplayName("Excel range без конечной строки читает B4:H до конца данных листа")]
    public async Task Range_without_end_row_reads_until_sheet_ends()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var fileName = "sparse-range-open-ended.xlsx";
            await WriteSparseWorkbookAsync(tempDirectory, fileName);
            var source = new FileSystemSource(tempDirectory);

            var rawReader = await Provider.OpenReaderAsync(
                source,
                new ExcelTableConfig
                {
                    FileName = fileName,
                    WorksheetName = "Sales",
                    HasHeader = true,
                    Range = new ExcelCellRange
                    {
                        StartRow = 4,
                        StartColumn = 2,
                        EndColumn = 8
                    }
                });
            await using var reader = rawReader.Normalize();

            await Assert.That(reader).HaveData(
                columns: ["Date", "Region", "Manager", "Product", "Quantity", "Price", "Revenue"],
                types: [DataType.Text, DataType.Text, DataType.Text, DataType.Text, DataType.Text, DataType.Text, DataType.Text],
                rows: [
                    ("2026-08-01", "North", "Anna", "Laptop", "4", "899", "3596"),
                    ("2026-08-03", "West", "Ilya", "Monitor", "7", "279.5", "1956.5"),
                    ("2026-08-06", "South", "Maria", "Keyboard", "12", "64.9", "778.8"),
                    ("2026-08-09", "East", "Oleg", "Mouse", "20", "29.9", "598"),
                    ("2026-08-13", "North", "Anna", "Dock", "5", "159", "795"),
                    ("2026-08-18", "West", "Ilya", "Laptop", "3", "949", "2847")
                ]);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    [DisplayName("Excel range с пустым header cell генерирует fallback имя колонки")]
    public async Task Range_empty_header_cell_uses_generated_column_name()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var fileName = "range-empty-header.xlsx";
            await WriteWorkbookAsync(
                tempDirectory,
                fileName,
                ("empty_header", CreateTable(
                    columns: [("ignored_a", typeof(string)), ("ignored_b", typeof(string)), ("ignored_c", typeof(string))],
                    rows: [
                        ["skip", DBNull.Value, "name"],
                        ["skip", "1", "Alice"]
                    ])));

            var source = new FileSystemSource(tempDirectory);

            var rawReader = await Provider.OpenReaderAsync(
                source,
                new ExcelTableConfig
                {
                    FileName = fileName,
                    WorksheetName = "empty_header",
                    HasHeader = true,
                    Range = new ExcelCellRange
                    {
                        StartRow = 2,
                        StartColumn = 2,
                        EndRow = 3,
                        EndColumn = 3
                    }
                });
            await using var reader = rawReader.Normalize();

            await Assert.That(reader).HaveData(
                columns: ["B", "name"],
                types: [DataType.Text, DataType.Text],
                rows: [
                    ("1", "Alice")
                ]);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    [DisplayName("Excel range шире строки возвращает DBNull для отсутствующих ячеек справа")]
    public async Task Range_returns_db_null_for_missing_cells_on_the_right()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var fileName = "range-missing-right-cells.xlsx";
            await WriteWorkbookAsync(
                tempDirectory,
                fileName,
                ("missing_right", CreateTable(
                    columns: [("id", typeof(string)), ("name", typeof(string))],
                    rows: [
                        ["1", "Alice"]
                    ])));

            var source = new FileSystemSource(tempDirectory);

            var rawReader = await Provider.OpenReaderAsync(
                source,
                new ExcelTableConfig
                {
                    FileName = fileName,
                    WorksheetName = "missing_right",
                    HasHeader = true,
                    Range = new ExcelCellRange
                    {
                        StartRow = 1,
                        StartColumn = 1,
                        EndRow = 2,
                        EndColumn = 4
                    }
                });
            await using var reader = rawReader.Normalize();

            await Assert.That(reader).HaveData(
                columns: ["id", "name", "C", "D"],
                types: [DataType.Text, DataType.Text, DataType.Text, DataType.Text],
                rows: [
                    ("1", "Alice", DBNull.Value, DBNull.Value)
                ]);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "Loader.Core.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static FileSystemSource CreateFixtureSource()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Excel");
        return new FileSystemSource(directory);
    }

    private static async Task WriteWorkbookAsync(
        string directory,
        string fileName,
        params (string SheetName, DataTable Table)[] sheets)
    {
        var path = Path.Combine(directory, fileName);
        await using var writer = await ExcelDataWriter.CreateAsync(path);

        foreach (var sheet in sheets)
        {
            using var reader = sheet.Table.CreateDataReader();
            await writer.WriteAsync(reader, sheet.SheetName);
        }
    }

    private static async Task WriteSparseWorkbookAsync(
        string directory,
        string fileName)
    {
        var path = Path.Combine(directory, fileName);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);

        await WriteEntryAsync(
            archive,
            "[Content_Types].xml",
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Default Extension="xml" ContentType="application/xml"/>
              <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
              <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
            </Types>
            """);
        await WriteEntryAsync(
            archive,
            "_rels/.rels",
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
            </Relationships>
            """);
        await WriteEntryAsync(
            archive,
            "xl/workbook.xml",
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <sheets>
                <sheet name="Sales" sheetId="1" r:id="rId1"/>
              </sheets>
            </workbook>
            """);
        await WriteEntryAsync(
            archive,
            "xl/_rels/workbook.xml.rels",
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
            </Relationships>
            """);
        await WriteEntryAsync(
            archive,
            "xl/worksheets/sheet1.xml",
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
              <dimension ref="B4:H10"/>
              <sheetData>
                <row r="4"><c r="B4" t="inlineStr"><is><t>Date</t></is></c><c r="C4" t="inlineStr"><is><t>Region</t></is></c><c r="D4" t="inlineStr"><is><t>Manager</t></is></c><c r="E4" t="inlineStr"><is><t>Product</t></is></c><c r="F4" t="inlineStr"><is><t>Quantity</t></is></c><c r="G4" t="inlineStr"><is><t>Price</t></is></c><c r="H4" t="inlineStr"><is><t>Revenue</t></is></c></row>
                <row r="5"><c r="B5" t="inlineStr"><is><t>2026-08-01</t></is></c><c r="C5" t="inlineStr"><is><t>North</t></is></c><c r="D5" t="inlineStr"><is><t>Anna</t></is></c><c r="E5" t="inlineStr"><is><t>Laptop</t></is></c><c r="F5"><v>4</v></c><c r="G5"><v>899</v></c><c r="H5"><v>3596</v></c></row>
                <row r="6"><c r="B6" t="inlineStr"><is><t>2026-08-03</t></is></c><c r="C6" t="inlineStr"><is><t>West</t></is></c><c r="D6" t="inlineStr"><is><t>Ilya</t></is></c><c r="E6" t="inlineStr"><is><t>Monitor</t></is></c><c r="F6"><v>7</v></c><c r="G6"><v>279.5</v></c><c r="H6"><v>1956.5</v></c></row>
                <row r="7"><c r="B7" t="inlineStr"><is><t>2026-08-06</t></is></c><c r="C7" t="inlineStr"><is><t>South</t></is></c><c r="D7" t="inlineStr"><is><t>Maria</t></is></c><c r="E7" t="inlineStr"><is><t>Keyboard</t></is></c><c r="F7"><v>12</v></c><c r="G7"><v>64.9</v></c><c r="H7"><v>778.8</v></c></row>
                <row r="8"><c r="B8" t="inlineStr"><is><t>2026-08-09</t></is></c><c r="C8" t="inlineStr"><is><t>East</t></is></c><c r="D8" t="inlineStr"><is><t>Oleg</t></is></c><c r="E8" t="inlineStr"><is><t>Mouse</t></is></c><c r="F8"><v>20</v></c><c r="G8"><v>29.9</v></c><c r="H8"><v>598</v></c></row>
                <row r="9"><c r="B9" t="inlineStr"><is><t>2026-08-13</t></is></c><c r="C9" t="inlineStr"><is><t>North</t></is></c><c r="D9" t="inlineStr"><is><t>Anna</t></is></c><c r="E9" t="inlineStr"><is><t>Dock</t></is></c><c r="F9"><v>5</v></c><c r="G9"><v>159</v></c><c r="H9"><v>795</v></c></row>
                <row r="10"><c r="B10" t="inlineStr"><is><t>2026-08-18</t></is></c><c r="C10" t="inlineStr"><is><t>West</t></is></c><c r="D10" t="inlineStr"><is><t>Ilya</t></is></c><c r="E10" t="inlineStr"><is><t>Laptop</t></is></c><c r="F10"><v>3</v></c><c r="G10"><v>949</v></c><c r="H10"><v>2847</v></c></row>
              </sheetData>
            </worksheet>
            """);
    }

    private static async Task WriteEntryAsync(
        ZipArchive archive,
        string name,
        string content)
    {
        var entry = archive.CreateEntry(name);
        await using var stream = entry.Open();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(content);
    }

    private static DataTable CreateTable(
        (string Name, Type Type)[] columns,
        object?[][] rows)
    {
        var table = new DataTable();
        foreach (var column in columns)
        {
            table.Columns.Add(column.Name, column.Type);
        }

        foreach (var row in rows)
        {
            table.Rows.Add(row);
        }

        return table;
    }
}

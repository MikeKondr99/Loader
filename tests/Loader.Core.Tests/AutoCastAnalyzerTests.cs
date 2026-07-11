using System.Data;
using Loader.Core.Tests.Infrastructure;

namespace Loader.Core.Tests;

public sealed class AutoCastAnalyzerTests
{
    [Test]
    [MethodDataSource(nameof(SingleValueCases))]
    [DisplayName("Analyzer для одного значения выбирает ожидаемый default format")]
    public async Task Single_value_selects_expected_format(string value, string expectedFormatName)
    {
        var actual = await AnalyzeFormatName(value);

        await Assert.That(actual).IsEqualTo(expectedFormatName);
    }

    [Test]
    [MethodDataSource(nameof(MultipleValueCases))]
    [DisplayName("Analyzer выбирает формат только если он подходит всем значениям колонки")]
    public async Task All_values_must_match_selected_format(string values, string expectedFormatName)
    {
        var actual = await AnalyzeFormatName(values.Split('|'));

        await Assert.That(actual).IsEqualTo(expectedFormatName);
    }

    [Test]
    [DisplayName("Analyzer DBNull не исключает числовой формат")]
    public async Task DbNull_does_not_disqualify_format()
    {
        var actual = await AnalyzeFormatName("1", DBNull.Value, "2");

        await Assert.That(actual).IsEqualTo("Integer");
    }

    [Test]
    [DisplayName("Analyzer text колонку где все значения DBNull оставляет Text")]
    public async Task Text_column_with_only_dbnull_values_selects_text()
    {
        var actual = await AnalyzeFormatName(DBNull.Value, DBNull.Value);

        await Assert.That(actual).IsEqualTo("Text");
    }

    [Test]
    [DisplayName("Analyzer не выдает format для уже типизированных колонок")]
    public async Task Already_typed_columns_are_not_analyzed()
    {
        using var table = new DataTable();
        table.Columns.Add("id", typeof(int));
        table.Columns.Add("created_at", typeof(DateTime));
        table.Columns.Add("amount_text", typeof(string));
        table.Rows.Add(1, new DateTime(2026, 1, 2), "10.50");

        using var rawReader = table.CreateDataReader();
        var analyzer = new AutoCastAnalyzer();
        await using var analyzeReader = rawReader
            .Normalize()
            .CollectAutoCast(analyzer);

        while (await analyzeReader.ReadAsync())
        {
        }

        await Assert.That(analyzer.Success).IsTrue();
        await Assert.That(analyzer.Schema).IsNotNull();
        await Assert.That(analyzer.Schema!.Fields.Select(field => field.Name).ToArray())
            .IsEquivalentTo(["amount_text"]);
        await Assert.That(analyzer.Schema.Fields[0].Format.Name)
            .IsEqualTo("Number(decimal='.', group=',')");
    }

    [Test]
    [DisplayName("AutoCast schema из analyzer можно применить вторым проходом")]
    public async Task Analyzer_schema_can_be_applied_on_second_pass()
    {
        using var analyzeTable = CreateTextTable("id", "created");
        analyzeTable.Rows.Add("1", "2026-01-02");
        analyzeTable.Rows.Add("2", "2026-02-03");

        using (var rawReader = analyzeTable.CreateDataReader())
        {
            var analyzer = new AutoCastAnalyzer();
            await using var analyzeReader = rawReader
                .Normalize()
                .CollectAutoCast(analyzer);

            while (await analyzeReader.ReadAsync())
            {
            }

            await Assert.That(analyzer.Success).IsTrue();
            await Assert.That(analyzer.Schema).IsNotNull();

            using var readTable = CreateTextTable("id", "created");
            readTable.Rows.Add("1", "2026-01-02");
            readTable.Rows.Add("2", "2026-02-03");

            using var secondRawReader = readTable.CreateDataReader();
            await using var reader = secondRawReader
                .Normalize()
                .AutoCast(analyzer.Schema!);

            await Assert.That(reader).HaveData(
                columns: ["id", "created"],
                types: [DataType.Integer, DataType.Date],
                rows: [
                    (1L, new DateOnly(2026, 1, 2)),
                    (2L, new DateOnly(2026, 2, 3))
                ]);
        }
    }

    public static IEnumerable<(string Value, string ExpectedFormatName)> SingleValueCases()
    {
        yield return ("42", "Integer");
        yield return ("-42", "Integer");
        yield return (" 42 ", "Integer");
        yield return ("0", "Integer");
        yield return ("+42", "Integer");

        yield return ("10.50", "Number(decimal='.', group=',')");
        yield return ("-10.50", "Number(decimal='.', group=',')");
        yield return ("1,234.56", "Number(decimal='.', group=',')");
        yield return ("0.0001", "Number(decimal='.', group=',')");

        yield return ("10,50", "Number(decimal=',', group=' ')");
        yield return ("-10,50", "Number(decimal=',', group=' ')");
        yield return ("1 234,56", "Number(decimal=',', group=' ')");
        yield return ("0,0001", "Number(decimal=',', group=' ')");

        yield return ("true", "Text");
        yield return ("false", "Text");
        yield return ("True", "Text");
        yield return ("FALSE", "Text");

        yield return ("2026-01-02", "Date(yyyy-MM-dd)");
        yield return ("02.01.2026", "Date(dd.MM.yyyy)");
        yield return ("2026-01-02 03:04:05", "DateTime(yyyy-MM-dd HH:mm:ss)");
        yield return ("2026-01-02T03:04:05", "DateTime(yyyy-MM-dd'T'HH:mm:ss)");
        yield return ("03:04:05", "Time(HH:mm:ss)");

        yield return (string.Empty, "Text");
        yield return ("hello", "Text");
        yield return ("42kg", "Text");
        yield return ("1 000", "Number(decimal=',', group=' ')");
        yield return ("1e3", "Text");
        yield return ("10.5e2", "Text");
        yield return ("2026/01/02", "Text");
        yield return ("01/02/2026", "Text");
        yield return ("2026-01-02Z", "Text");
        yield return ("2026-01-02T03:04:05Z", "Text");
        yield return ("2026-01-02T03:04:05+03:00", "Text");
        yield return ("03:04:05Z", "Text");
        yield return ("yes", "Text");
        yield return ("no", "Text");
        yield return ("-1", "Integer");
    }

    public static IEnumerable<(string Values, string ExpectedFormatName)> MultipleValueCases()
    {
        yield return ("1|2|3", "Integer");
        yield return ("1|2.5|3", "Number(decimal='.', group=',')");
        yield return ("1,234.56|5,678.90", "Number(decimal='.', group=',')");
        yield return ("1|2,5|3", "Number(decimal=',', group=' ')");
        yield return ("1 234,56|5 678,90", "Number(decimal=',', group=' ')");
        yield return ("true|false|TRUE", "Text");
        yield return ("2026-01-02|2026-02-03", "Date(yyyy-MM-dd)");
        yield return ("02.01.2026|03.02.2026", "Date(dd.MM.yyyy)");
        yield return ("2026-01-02 03:04:05|2026-02-03 04:05:06", "DateTime(yyyy-MM-dd HH:mm:ss)");
        yield return ("2026-01-02T03:04:05|2026-02-03T04:05:06", "DateTime(yyyy-MM-dd'T'HH:mm:ss)");
        yield return ("03:04:05|04:05:06", "Time(HH:mm:ss)");

        yield return ("1|", "Text");
        yield return ("1|two", "Text");
        yield return ("true|0", "Text");
        yield return ("1,234.56|1 234,56", "Text");
        yield return ("10.50|10,50", "Text");
        yield return ("2026-01-02|03.02.2026", "Text");
        yield return ("2026-01-02 03:04:05|2026-01-02T03:04:05", "Text");
        yield return ("03:04:05|03:04", "Text");
    }

    private static async Task<string> AnalyzeFormatName(params object[] values)
    {
        using var table = CreateTextTable("value");
        foreach (var value in values)
        {
            table.Rows.Add(value);
        }

        using var rawReader = table.CreateDataReader();
        var analyzer = new AutoCastAnalyzer();
        await using var analyzeReader = rawReader
            .Normalize()
            .CollectAutoCast(analyzer);

        while (await analyzeReader.ReadAsync())
        {
        }

        await Assert.That(analyzer.Success).IsTrue();
        await Assert.That(analyzer.Schema).IsNotNull();
        return analyzer.Schema!.Fields[0].Format.Name;
    }

    private static DataTable CreateTextTable(params string[] columns)
    {
        var table = new DataTable();
        foreach (var column in columns)
        {
            table.Columns.Add(column, typeof(string));
        }

        return table;
    }
}

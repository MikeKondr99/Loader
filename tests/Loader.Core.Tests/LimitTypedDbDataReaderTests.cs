using System.Data;
using Loader.Core.Data;
using Loader.Core.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Loader.Core.Tests;

public sealed class LimitDomainDataReaderTests
{
    [Test]
    [DisplayName("Normalize с Limit ограничивает количество строк и сохраняет схему")]
    public async Task Normalize_limit_limits_rows_and_preserves_schema()
    {
        using var table = CreateTable();
        table.Rows.Add(1, "Moscow");
        table.Rows.Add(2, "London");
        table.Rows.Add(3, "Berlin");

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader.Normalize(new NormalizeConfig
        {
            Limit = 1
        });

        await Assert.That(reader).HaveData(
            columns: ["id", "city"],
            types: [DataType.Integer, DataType.Text],
            rows: [
                (1L, "Moscow")
            ]);
    }

    [Test]
    [DisplayName("Limit как pipeline метод ограничивает строки после Where")]
    public async Task Pipeline_limit_limits_rows_after_where()
    {
        using var table = CreateTable();
        table.Rows.Add(1, "Moscow");
        table.Rows.Add(2, "Moscow");
        table.Rows.Add(3, "London");

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader
            .Normalize(new NormalizeConfig
            {
                Limit = null
            })
            .Where(row => row.Text("city") == "Moscow")
            .Limit(1);

        await Assert.That(reader).HaveData(
            columns: ["id", "city"],
            types: [DataType.Integer, DataType.Text],
            rows: [
                (1L, "Moscow")
            ]);
    }

    [Test]
    [DisplayName("Normalize с Limit до Where ограничивает исходный поток до фильтрации")]
    public async Task Normalize_limit_is_applied_before_pipeline_where()
    {
        using var table = CreateTable();
        table.Rows.Add(1, "Moscow");
        table.Rows.Add(2, "London");
        table.Rows.Add(3, "Berlin");

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader
            .Normalize(new NormalizeConfig
            {
                Limit = 2
            })
            .Where(row => row.Integer("id") > 2);

        await Assert.That(reader).HaveData(
            columns: ["id", "city"],
            types: [DataType.Integer, DataType.Text],
            rows: []);
    }

    [Test]
    [DisplayName("Limit больше количества строк читает весь доступный поток")]
    public async Task Limit_greater_than_source_rows_reads_all_rows()
    {
        using var table = CreateTable();
        table.Rows.Add(1, "Moscow");
        table.Rows.Add(2, "London");

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader
            .AsTyped()
            .Limit(10);

        await Assert.That(reader).HaveData(
            columns: ["id", "city"],
            types: [DataType.Integer, DataType.Text],
            rows: [
                (1L, "Moscow"),
                (2L, "London")
            ]);
    }

    [Test]
    [DisplayName("Normalize с Limit 0 сохраняет схему и возвращает пустой поток")]
    public async Task Normalize_zero_limit_returns_empty_stream()
    {
        using var table = CreateTable();
        table.Rows.Add(1, "Moscow");

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader.Normilize(new NormalizeConfig
        {
            Limit = 0
        });

        await Assert.That(reader).HaveData(
            columns: ["id", "city"],
            types: [DataType.Integer, DataType.Text],
            rows: []);
    }

    [Test]
    [DisplayName("Limit с отрицательным значением кидает ошибку конфигурации")]
    public async Task Negative_limit_throws()
    {
        using var table = CreateTable();
        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader.AsTyped();

        await Assert.That(() => reader.Limit(-1))
            .ThrowsExactly<ArgumentOutOfRangeException>()
            .WithMessage("Limit must be greater than or equal to zero. (Parameter 'count')\r\nActual value was -1.");
    }

    [Test]
    [DisplayName("Normalize с отрицательным Limit кидает ошибку конфигурации")]
    public async Task Normalize_negative_limit_throws()
    {
        using var table = CreateTable();
        using var rawReader = table.CreateDataReader();

        await Assert.That(() => rawReader.Normalize(new NormalizeConfig
            {
                Limit = -1
            }))
            .ThrowsExactly<ArgumentOutOfRangeException>()
            .WithMessage("Limit must be greater than or equal to zero. (Parameter 'Limit')\r\nActual value was -1.");
    }

    [Test]
    [DisplayName("Limit через ReadAsync ограничивает количество строк")]
    public async Task Limit_works_with_read_async()
    {
        using var table = CreateTable();
        table.Rows.Add(1, "Moscow");
        table.Rows.Add(2, "London");

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader
            .AsTyped()
            .Limit(1);

        await Assert.That(await reader.ReadAsync()).IsTrue();
        await Assert.That(reader.GetValue(0)).IsEqualTo(1L);
        await Assert.That(await reader.ReadAsync()).IsFalse();
    }

    private static DataTable CreateTable()
    {
        var table = new DataTable();
        table.Columns.Add("id", typeof(int));
        table.Columns.Add("city", typeof(string));
        return table;
    }
}

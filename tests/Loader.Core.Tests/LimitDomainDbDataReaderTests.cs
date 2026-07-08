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
    [DisplayName("Limit как pipeline метод ограничивает строки после Where")]
    public async Task Pipeline_limit_limits_rows_after_where()
    {
        using var table = CreateTable();
        table.Rows.Add(1, "Moscow");
        table.Rows.Add(2, "Moscow");
        table.Rows.Add(3, "London");

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader
            .Normalize()
            .Where(row => row.Text("city") == "Moscow")
            .Limit(1);

        await Assert.That(reader).HaveData(
            columns: ["id", "city"],
            types: [DataType.Integer, DataType.Text],
            rows: [
                (1, "Moscow")
            ]);
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
            .Normalize()
            .Limit(10);

        await Assert.That(reader).HaveData(
            columns: ["id", "city"],
            types: [DataType.Integer, DataType.Text],
            rows: [
                (1, "Moscow"),
                (2, "London")
            ]);
    }

    [Test]
    [DisplayName("Limit с отрицательным значением кидает ошибку конфигурации")]
    public async Task Negative_limit_throws()
    {
        using var table = CreateTable();
        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader.Normalize();

        await Assert.That(() => reader.Limit(-1))
            .ThrowsExactly<ArgumentOutOfRangeException>()
            .WithMessage("Limit must be greater than or equal to zero. (Parameter 'count')\r\nActual value was -1.");
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
            .Normalize()
            .Limit(1);

        await Assert.That(await reader.ReadAsync()).IsTrue();
        await Assert.That(reader.GetValue(0)).IsEqualTo(1);
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

using System.Data;
using Loader.Core.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Loader.Core.Tests;

public sealed class DataMetaContainerTests
{
    [Test]
    [DisplayName("CollectMeta после полного чтения собирает статистику колонок и Success true")]
    public async Task Collects_column_meta_after_full_read()
    {
        using var table = CreateTable();
        table.Rows.Add(1, 10.50m, "Moscow");
        table.Rows.Add(2, DBNull.Value, "London");
        table.Rows.Add(2, 20.25m, "Moscow");
        var meta = new DataMetaContainer();

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader
            .Normalize()
            .CollectMeta(meta);

        await Assert.That(meta.Success).IsFalse();
        await Assert.That(reader).HaveData(
            columns: ["id", "amount", "city"],
            types: [DataType.Integer, DataType.Number, DataType.Text],
            rows: [
                (1, 10.50m, "Moscow"),
                (2, DBNull.Value, "London"),
                (2, 20.25m, "Moscow")
            ]);

        await Assert.That(meta.Success).IsTrue();
        await Assert.That(meta.RowCount).IsEqualTo(3);

        await Assert.That(meta.Columns[0].Name).IsEqualTo("id");
        await Assert.That(meta.Columns[0].UniqueValueCount).IsEqualTo(2);
        await Assert.That(meta.Columns[0].AllValuesUnique).IsFalse();
        await Assert.That(meta.Columns[0].Density).IsEqualTo(1m);
        await Assert.That(meta.Columns[0].Min).IsEqualTo(1m);
        await Assert.That(meta.Columns[0].Max).IsEqualTo(2m);

        await Assert.That(meta.Columns[1].Name).IsEqualTo("amount");
        await Assert.That(meta.Columns[1].UniqueValueCount).IsEqualTo(3);
        await Assert.That(meta.Columns[1].AllValuesUnique).IsTrue();
        await Assert.That(meta.Columns[1].Density).IsEqualTo(2m / 3m);
        await Assert.That(meta.Columns[1].Min).IsEqualTo(10.50m);
        await Assert.That(meta.Columns[1].Max).IsEqualTo(20.25m);

        await Assert.That(meta.Columns[2].Name).IsEqualTo("city");
        await Assert.That(meta.Columns[2].UniqueValueCount).IsEqualTo(2);
        await Assert.That(meta.Columns[2].AllValuesUnique).IsFalse();
        await Assert.That(meta.Columns[2].Density).IsEqualTo(1m);
        await Assert.That(meta.Columns[2].Min).IsNull();
        await Assert.That(meta.Columns[2].Max).IsNull();
    }

    [Test]
    [DisplayName("CollectMeta после Where собирает статистику только отфильтрованных строк")]
    public async Task Collects_meta_after_where_only_for_filtered_rows()
    {
        using var table = CreateTable();
        table.Rows.Add(1, 10m, "Moscow");
        table.Rows.Add(2, 20m, "London");
        table.Rows.Add(3, 30m, "Moscow");
        var meta = new DataMetaContainer();

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader
            .Normalize()
            .Where(row => row.Text("city") == "Moscow")
            .CollectMeta(meta);

        await Assert.That(reader).HaveData(
            columns: ["id", "amount", "city"],
            types: [DataType.Integer, DataType.Number, DataType.Text],
            rows: [
                (1, 10m, "Moscow"),
                (3, 30m, "Moscow")
            ]);

        await Assert.That(meta.Success).IsTrue();
        await Assert.That(meta.RowCount).IsEqualTo(2);
        await Assert.That(meta.Columns[0].Min).IsEqualTo(1m);
        await Assert.That(meta.Columns[0].Max).IsEqualTo(3m);
        await Assert.That(meta.Columns[2].UniqueValueCount).IsEqualTo(1);
        await Assert.That(meta.Columns[2].AllValuesUnique).IsFalse();
    }

    [Test]
    [DisplayName("CollectMeta после Limit собирает статистику только ограниченного потока")]
    public async Task Collects_meta_after_limit_only_for_limited_rows()
    {
        using var table = CreateTable();
        table.Rows.Add(1, 10m, "Moscow");
        table.Rows.Add(2, 20m, "London");
        table.Rows.Add(3, 30m, "Berlin");
        var meta = new DataMetaContainer();

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader
            .Normalize()
            .Limit(2)
            .CollectMeta(meta);

        await Assert.That(reader).HaveData(
            columns: ["id", "amount", "city"],
            types: [DataType.Integer, DataType.Number, DataType.Text],
            rows: [
                (1, 10m, "Moscow"),
                (2, 20m, "London")
            ]);

        await Assert.That(meta.Success).IsTrue();
        await Assert.That(meta.RowCount).IsEqualTo(2);
        await Assert.That(meta.Columns[0].Max).IsEqualTo(2m);
        await Assert.That(meta.Columns[1].Max).IsEqualTo(20m);
        await Assert.That(meta.Columns[2].UniqueValueCount).IsEqualTo(2);
    }

    [Test]
    [DisplayName("CollectMeta MaxCardinality ограничивает хранение уникальных значений")]
    public async Task Max_cardinality_limits_unique_value_storage()
    {
        using var table = CreateTable();
        table.Rows.Add(1, 10m, "Moscow");
        table.Rows.Add(2, 20m, "London");
        table.Rows.Add(3, 30m, "Berlin");
        var meta = new DataMetaContainer(new DataMetaOptions
        {
            MaxCardinality = 2
        });

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader
            .Normalize()
            .CollectMeta(meta);

        while (await reader.ReadAsync())
        {
        }

        await Assert.That(meta.Success).IsTrue();
        await Assert.That(meta.Columns[0].CardinalityExceeded).IsTrue();
        await Assert.That(meta.Columns[0].UniqueValueCount).IsEqualTo(0);
        await Assert.That(meta.Columns[2].CardinalityExceeded).IsTrue();
        await Assert.That(meta.Columns[2].UniqueValueCount).IsEqualTo(0);
    }

    [Test]
    [DisplayName("CollectMeta MaxCardinality 0 отключает хранение уникальных значений")]
    public async Task Max_cardinality_zero_disables_unique_value_storage()
    {
        using var table = CreateTable();
        table.Rows.Add(1, 10m, "Moscow");
        table.Rows.Add(2, 20m, "London");
        var meta = new DataMetaContainer(new DataMetaOptions
        {
            MaxCardinality = 0
        });

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader
            .Normalize()
            .CollectMeta(meta);

        while (await reader.ReadAsync())
        {
        }

        await Assert.That(meta.Success).IsTrue();
        await Assert.That(meta.Columns[0].CardinalityExceeded).IsTrue();
        await Assert.That(meta.Columns[0].UniqueValueCount).IsEqualTo(0);
        await Assert.That(meta.Columns[0].Min).IsEqualTo(1m);
        await Assert.That(meta.Columns[0].Max).IsEqualTo(2m);
        await Assert.That(meta.Columns[2].Density).IsEqualTo(1m);
    }

    [Test]
    [DisplayName("CollectMeta на пустом потоке сохраняет схему и завершает Success true")]
    public async Task Empty_stream_completes_meta_with_schema()
    {
        using var table = CreateTable();
        var meta = new DataMetaContainer();

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader
            .Normalize()
            .CollectMeta(meta);

        await Assert.That(reader).HaveData(
            columns: ["id", "amount", "city"],
            types: [DataType.Integer, DataType.Number, DataType.Text],
            rows: []);

        await Assert.That(meta.Success).IsTrue();
        await Assert.That(meta.RowCount).IsEqualTo(0);
        await Assert.That(meta.Columns.Count).IsEqualTo(3);
        await Assert.That(meta.Columns[0].UniqueValueCount).IsEqualTo(0);
        await Assert.That(meta.Columns[0].AllValuesUnique).IsTrue();
        await Assert.That(meta.Columns[0].Density).IsEqualTo(0m);
        await Assert.That(meta.Columns[0].Min).IsNull();
        await Assert.That(meta.Columns[0].Max).IsNull();
    }

    [Test]
    [DisplayName("CollectMeta через ReadAsync после полного чтения выставляет Success true")]
    public async Task Collects_meta_with_read_async()
    {
        using var table = CreateTable();
        table.Rows.Add(1, 10m, "Moscow");
        table.Rows.Add(2, DBNull.Value, "London");
        var meta = new DataMetaContainer();

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader
            .Normalize()
            .CollectMeta(meta);

        await Assert.That(await reader.ReadAsync()).IsTrue();
        await Assert.That(await reader.ReadAsync()).IsTrue();
        await Assert.That(await reader.ReadAsync()).IsFalse();
        await Assert.That(meta.Success).IsTrue();
        await Assert.That(meta.RowCount).IsEqualTo(2);
        await Assert.That(meta.Columns[1].Density).IsEqualTo(1m / 2m);
    }

    [Test]
    [DisplayName("CollectMeta после частичного чтения оставляет Success false")]
    public async Task Partial_read_keeps_success_false()
    {
        using var table = CreateTable();
        table.Rows.Add(1, 10m, "Moscow");
        table.Rows.Add(2, 20m, "London");
        var meta = new DataMetaContainer();

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader
            .Normalize()
            .CollectMeta(meta);

        await Assert.That(reader.Read()).IsTrue();
        await Assert.That(meta.Success).IsFalse();
        await Assert.That(meta.RowCount).IsEqualTo(1);
    }

    [Test]
    [DisplayName("CollectMeta при ошибке дальше в pipeline оставляет Success false")]
    public async Task Pipeline_error_keeps_success_false()
    {
        using var table = CreateTable();
        table.Rows.Add(1, 10m, "Moscow");
        var meta = new DataMetaContainer();

        using var rawReader = table.CreateDataReader();
        await using var reader = rawReader
            .Normalize()
            .CollectMeta(meta)
            .Where(row => row.Text("missing") == "value");

        await Assert.That(() => reader.Read())
            .ThrowsExactly<IndexOutOfRangeException>()
            .WithMessage("Column 'missing' was not found.");
        await Assert.That(meta.Success).IsFalse();
    }

    private static DataTable CreateTable()
    {
        var table = new DataTable();
        table.Columns.Add("id", typeof(int));
        table.Columns.Add("amount", typeof(decimal));
        table.Columns.Add("city", typeof(string));
        return table;
    }
}

using System.Data;
using Loader.Core.Decorators;

namespace Loader.Core.Tests;

public sealed class RenameColumnDataReaderTests
{
    [Test]
    public async Task Rename_reader_uses_provided_names_and_keeps_original_names()
    {
        using var table = CreateTable();
        using var inner = table.CreateDataReader();
        using var reader = inner.RenameColumns(["column1", "column2"]);

        await Assert.That(reader.OriginalNames).Count().IsEqualTo(2);
        await Assert.That(reader.OriginalNames[0]).IsEqualTo("id");
        await Assert.That(reader.OriginalNames[1]).IsEqualTo("name");
        await Assert.That(reader.GetName(0)).IsEqualTo("column1");
        await Assert.That(reader.GetName(1)).IsEqualTo("column2");
        await Assert.That(reader.GetOrdinal("column1")).IsEqualTo(0);
        await Assert.That(reader.GetOrdinal("column2")).IsEqualTo(1);
    }

    [Test]
    public async Task Abstract_columns_uses_column_number_names()
    {
        using var table = CreateTable();
        using var inner = table.CreateDataReader();
        using var reader = inner.AbstractColumns();

        await Assert.That(reader.GetName(0)).IsEqualTo("column1");
        await Assert.That(reader.GetName(1)).IsEqualTo("column2");
    }

    [Test]
    public async Task Abstract_columns_grows_cached_names_when_reader_has_more_than_default_count()
    {
        using var table = CreateTable(31);
        using var inner = table.CreateDataReader();
        using var reader = inner.AbstractColumns();

        await Assert.That(reader.GetName(0)).IsEqualTo("column1");
        await Assert.That(reader.GetName(29)).IsEqualTo("column30");
        await Assert.That(reader.GetName(30)).IsEqualTo("column31");
    }

    [Test]
    public async Task Rename_reader_keeps_values_and_typed_accessors()
    {
        using var table = CreateTable();
        using var inner = table.CreateDataReader();
        using var reader = inner.RenameColumns(["column1", "column2"]);

        await Assert.That(reader.Read()).IsTrue();
        await Assert.That(reader.GetInt32(0)).IsEqualTo(1);
        await Assert.That(reader.GetString(1)).IsEqualTo("Moscow");
        await Assert.That(reader.GetValue(0)).IsEqualTo(1);
        await Assert.That(reader.GetValue(1)).IsEqualTo("Moscow");
    }

    [Test]
    public async Task Rename_reader_rejects_name_count_not_matching_field_count()
    {
        using var table = CreateTable();
        using var inner = table.CreateDataReader();

        await Assert.That(() => new RenameColumnDataReader(inner, ["column1"]))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task Rename_reader_rejects_duplicate_names()
    {
        using var table = CreateTable();
        using var inner = table.CreateDataReader();

        await Assert.That(() => new RenameColumnDataReader(inner, ["column", "column"]))
            .ThrowsExactly<ArgumentException>();
    }

    private static DataTable CreateTable()
    {
        var table = new DataTable();
        table.Columns.Add("id", typeof(int));
        table.Columns.Add("name", typeof(string));
        table.Rows.Add(1, "Moscow");
        return table;
    }

    private static DataTable CreateTable(int fieldCount)
    {
        var table = new DataTable();
        for (var i = 1; i <= fieldCount; i++)
        {
            table.Columns.Add($"source{i}", typeof(string));
        }

        return table;
    }
}

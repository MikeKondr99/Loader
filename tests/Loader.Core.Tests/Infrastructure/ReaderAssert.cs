using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Text;
using TUnit.Assertions;
using TUnit.Assertions.Core;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;

namespace Loader.Core.Tests.Infrastructure;

internal static class ReaderAssert
{
    public static async Task HaveData(
        this IAssertionSource<DomainDataReader> source,
        string[] columns,
        DataType[] types,
        ITuple[] rows)
    {
        var (readerOrNull, exception) = await source.Context.GetAsync();

        if (exception is not null)
        {
            throw exception;
        }

        var reader = readerOrNull!;
        await Assert.That(reader).HaveSchema(columns, types);
        await Assert.That((DbDataReader)reader).HaveRows(columns, rows);
    }

    public static async Task HaveSchema(
        this IAssertionSource<DomainDataReader> source,
        string[] columns,
        DataType[] types)
    {
        var (readerOrNull, exception) = await source.Context.GetAsync();

        if (exception is not null)
        {
            throw exception;
        }

        var reader = readerOrNull!;
        await Assert.That(reader.FieldCount).IsEqualTo(columns.Length);
        await Assert.That(reader.DataSchema.Fields.Count).IsEqualTo(columns.Length);
        await Assert.That(types.Length).IsEqualTo(columns.Length);
        await Assert.That(Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray())
            .IsEquivalentTo(columns, CollectionOrdering.Matching);
        await Assert.That(reader.DataSchema.Fields.Select(field => field.Name).ToArray())
            .IsEquivalentTo(columns, CollectionOrdering.Matching);
        await Assert.That(reader.DataSchema.Fields.Select(field => field.DataType).ToArray())
            .IsEquivalentTo(types, CollectionOrdering.Matching);

        for (var i = 0; i < columns.Length; i++)
        {
            await Assert.That(reader.GetName(i)).IsEqualTo(columns[i]);
            await Assert.That(reader.DataSchema.Fields[i].Name).IsEqualTo(columns[i]);
            await Assert.That(reader.DataSchema.Fields[i].DataType).IsEqualTo(types[i]);
            await Assert.That(reader.GetFieldType(i)).IsEqualTo(reader.DataSchema.Fields[i].ClrType);
        }
    }

    public static async Task HaveRows(
        this IAssertionSource<DbDataReader> source,
        string[] columns,
        ITuple[] rows)
    {
        var (readerOrNull, exception) = await source.Context.GetAsync();

        if (exception is not null)
        {
            throw exception;
        }

        var reader = readerOrNull!;
        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            await Assert.That(reader.Read()).IsTrue();
            await ExpectRow(reader, columns, rows[rowIndex]);
        }

        await Assert.That(reader.Read()).IsFalse();
    }

    private static async Task ExpectRow(DbDataReader reader, string[] columns, ITuple row)
    {
        await Assert.That(row.Length).IsEqualTo(columns.Length);

        for (var i = 0; i < columns.Length; i++)
        {
            var expected = row[i];
            await Assert.That(reader.GetValue(i)).IsEqualTo(expected);
            await Assert.That(reader[columns[i]]).IsEqualTo(expected);
        }
    }


    public static async Task LogReader(DbDataReader reader) {
        var columns = (await reader.GetColumnSchemaAsync()).ToArray();
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Reader has {reader.FieldCount} columns:");
        for (int i = 0; i < reader.FieldCount; i++)
        {
            var column = columns[i];
            sb.AppendLine($@"
            [{i}] 
                Name: {column.ColumnName} DataType: {column.DataType} DataTypeName: {column.DataTypeName}
                AllowDBNull: {column.AllowDBNull} ColumnSize: {column.ColumnSize} NumericPrecision: {column.NumericPrecision}
                NumericScale: {column.NumericScale} IsReadOnly: {column.IsReadOnly} IsUnique: {column.IsUnique}
                IsKey: {column.IsKey} IsAutoIncrement: {column.IsAutoIncrement} IsLong: {column.IsLong}
                BaseTableName: {column.BaseTableName} BaseColumnName: {column.BaseColumnName} BaseSchemaName: {column.BaseSchemaName}
                BaseCatalogName: {column.BaseCatalogName}"); }
        await Assert.That(sb.ToString()).EqualTo("");
    }
    
}

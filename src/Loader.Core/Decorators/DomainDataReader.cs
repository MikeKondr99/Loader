using System.Data.Common;

namespace Loader.Core.Decorators;

/// <summary>
/// Base reader for domain-level streams. It exposes normalized schema and guarantees
/// that value access is valid only while the reader is positioned on a readable row.
/// </summary>
public abstract class DomainDataReader : DbDataReaderDecorator
{
    protected DomainDataReader(DbDataReader inner)
        : base(inner)
    {
    }

    public abstract DataSchema DataSchema { get; }

    protected bool HasReadableRow { get; set; }

    public override int FieldCount => DataSchema.Fields.Count;

    public override Type GetFieldType(int ordinal) => DataSchema.GetField(ordinal).ClrType;

    public override string GetName(int ordinal) => DataSchema.GetField(ordinal).Name;

    public override int GetOrdinal(string name) => DataSchema.GetOrdinal(name);

    public override bool IsDBNull(int ordinal)
    {
        return GetValue(ordinal) == DBNull.Value;
    }

    protected void EnsureReadableRow()
    {
        if (!HasReadableRow)
        {
            throw new InvalidOperationException("Reader is not positioned on a row.");
        }
    }
}

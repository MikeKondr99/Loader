using System.Collections.ObjectModel;
using System.Data.Common;

namespace Loader.Core.Decorators;

/// <summary>
/// Контейнер метаинформации, которая собирается во время чтения stream-а.
/// </summary>
public sealed class DataMetaContainer
{
    private readonly List<DataColumnMeta> _columns = [];
    private readonly DataMetaOptions _options;
    private bool _started;

    public DataMetaContainer()
        : this(DataMetaOptions.Default)
    {
    }

    public DataMetaContainer(DataMetaOptions options)
    {
        _options = options;
    }

    public bool Success { get; private set; }

    public long RowCount { get; private set; }

    public IReadOnlyList<DataColumnMeta> Columns => _columns;

    internal void Start(DataSchema schema, ReadOnlyCollection<DbColumn> columnSchema)
    {
        _columns.Clear();
        RowCount = 0;
        Success = false;
        _started = true;

        for (var ordinal = 0; ordinal < schema.Fields.Count; ordinal++)
        {
            var field = schema.Fields[ordinal];
            var dbColumn = ordinal < columnSchema.Count ? columnSchema[ordinal] : null;
            _columns.Add(new DataColumnMeta(
                field.Ordinal,
                field.Name,
                field.DataType,
                dbColumn?.NumericPrecision,
                dbColumn?.NumericScale,
                _options.MaxCardinality));
        }
    }

    internal void CollectRow(DomainDataReader reader)
    {
        if (!_started)
        {
            Start(reader.DataSchema, reader.GetColumnSchema());
        }

        RowCount++;

        for (var ordinal = 0; ordinal < _columns.Count; ordinal++)
        {
            _columns[ordinal].CollectValue(reader.GetValue(ordinal), RowCount);
        }
    }

    internal void Complete()
    {
        Success = true;
    }

    internal void Fail()
    {
        Success = false;
    }
}

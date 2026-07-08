using System.Data.Common;

namespace Loader.Core.Data;

/// <summary>
/// Нормализованная схема потока, которую библиотека использует поверх DbDataReader.
/// </summary>
public sealed record DataSchema
{
    private IReadOnlyDictionary<string, int>? _ordinalsByName;

    public required IReadOnlyList<DataField> Fields { get; init; }

    public IReadOnlyDictionary<string, int> OrdinalsByName
    {
        get
        {
            _ordinalsByName ??= Fields.ToDictionary(
                dataField => dataField.Name,
                dataField => dataField.Ordinal,
                StringComparer.Ordinal);

            return _ordinalsByName;
        }
    }

    public int GetOrdinal(string name)
    {
        if (OrdinalsByName.TryGetValue(name, out var ordinal))
        {
            return ordinal;
        }

        throw new IndexOutOfRangeException($"Column '{name}' was not found.");
    }

    public DataField GetField(int ordinal)
    {
        if (ordinal < 0 || ordinal >= Fields.Count)
        {
            throw new IndexOutOfRangeException($"Column ordinal {ordinal} is out of range.");
        }

        return Fields[ordinal];
    }

    internal static DataSchema FromReader(DbDataReader reader)
    {
        // 1. Берем имена и CLR-типы из reader.
        var fields = Enumerable
            .Range(0, reader.FieldCount)
            .Select(i =>
            {
                var mapping = DataValueMapper.MapType(reader.GetFieldType(i));
                return new DataField
                {
                    Ordinal = i,
                    Name = reader.GetName(i),
                    DataType = mapping.DataType,
                    ClrType = mapping.ClrType,
                    Convert = mapping.Convert
                };
            })
            .ToArray();

        // 2. Запрещаем неявно неоднозначную адресацию по имени.
        var duplicate = fields
            .GroupBy(field => field.Name, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new DuplicateDataFieldNameException(duplicate.Key);
        }

        return new DataSchema
        {
            Fields = fields
        };
    }
}

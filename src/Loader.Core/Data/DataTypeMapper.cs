namespace Loader.Core.Data;

/// <summary>
/// Фасад для получения канонического DataType по CLR-типу.
/// </summary>
public static class DataTypeMapper
{
    public static DataType FromClrType(Type type)
    {
        return DataValueConverter.FromClrType(type).DataType;
    }

    public static Type ToClrType(DataType dataType)
    {
        return DataValueConverter.FromDataType(dataType).CanonicalClrType;
    }
}

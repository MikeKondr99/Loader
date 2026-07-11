namespace Loader.Core.Models;

/// <summary>
/// Фасад для получения канонического DataType по CLR-типу.
/// </summary>
public static class DataTypeMapper
{
    public static DataType FromClrType(Type type)
    {
        return DataValueMapper.MapType(type).DataType;
    }

    public static Type ToClrType(DataType dataType)
    {
        return DataValueMapper.DefaultClrType(dataType);
    }
}

namespace Loader.Query.Models;

/// <summary>
/// Узкий набор доменных типов, с которым дальше работает query layer.
/// </summary>
public enum DataType
{
    Unknown,
    Null,
    Text,
    Integer,
    Number,
    DateTime,
    Date,
    Time,
    Boolean
}

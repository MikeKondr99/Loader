using Loader.Core.Abstractions;

namespace Loader.Core.Providers.Xml;

/// <summary>
/// Настройки чтения XML-элементов с именем <see cref="TableName"/> как строк таблицы.
/// </summary>
public sealed record XmlTableConfig : ITableConfig
{
    public required string FileName { get; init; }

    public required string TableName { get; init; }

    public required XmlTableSchema Schema { get; init; }
}

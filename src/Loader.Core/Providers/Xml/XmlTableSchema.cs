namespace Loader.Core.Providers.Xml;

/// <summary>
/// Схема плоской XML-таблицы в порядке колонок результата.
/// </summary>
public sealed record XmlTableSchema
{
    public required IReadOnlyList<XmlColumnSchema> Columns { get; init; }
}

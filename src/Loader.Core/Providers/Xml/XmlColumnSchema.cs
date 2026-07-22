namespace Loader.Core.Providers.Xml;

/// <summary>
/// Колонка плоской XML-таблицы. Путь <c>@name</c> обозначает атрибут строки,
/// остальные пути обозначают прямые дочерние элементы.
/// </summary>
public sealed record XmlColumnSchema
{
    public required string Name { get; init; }

    public required string Path { get; init; }
}

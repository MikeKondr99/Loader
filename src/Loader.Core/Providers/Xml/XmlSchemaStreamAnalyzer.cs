using System.Xml;
using Loader.Core.Exceptions;

namespace Loader.Core.Providers.Xml;

/// <summary>
/// Потоково выводит схему выбранной XML-таблицы без загрузки документа или строк в память.
/// В памяти остаются только уникальные имена колонок. Каждый элемент с именем таблицы считается
/// строкой; колонками становятся его атрибуты и прямые дочерние элементы без вложенных элементов.
/// </summary>
internal static class XmlSchemaStreamAnalyzer
{
    public static async ValueTask<XmlTableSchema> AnalyzeAsync(
        Stream stream,
        string fileName,
        string tableName,
        CancellationToken cancellationToken)
    {
        using var reader = XmlReaderFactory.Create(stream);
        var columns = new List<XmlColumnSchema>();
        var paths = new HashSet<string>(StringComparer.Ordinal);
        var foundTable = false;

        // 1. Проходим весь XML: поздние строки могут добавить новые колонки в union-схему.
        while (await ReadAsync(reader, cancellationToken).ConfigureAwait(false))
        {
            if (reader.NodeType != XmlNodeType.Element ||
                !string.Equals(reader.LocalName, tableName, StringComparison.Ordinal))
            {
                continue;
            }

            foundTable = true;
            await AnalyzeRowAsync(reader, columns, paths, cancellationToken).ConfigureAwait(false);
        }

        if (!foundTable)
        {
            throw new XmlTableNotFoundProviderException(fileName, tableName);
        }

        var schema = new XmlTableSchema { Columns = columns };
        XmlTableSchemaValidator.Validate(fileName, schema);
        return schema;
    }

    private static async ValueTask AnalyzeRowAsync(
        XmlReader reader,
        List<XmlColumnSchema> columns,
        HashSet<string> paths,
        CancellationToken cancellationToken)
    {
        var rowDepth = reader.Depth;

        // 1. Атрибуты строки добавляем до элементов и отмечаем путем @name.
        if (reader.MoveToFirstAttribute())
        {
            do
            {
                if (!IsNamespaceDeclaration(reader))
                {
                    AddColumn(columns, paths, reader.LocalName, $"@{reader.LocalName}");
                }
            }
            while (reader.MoveToNextAttribute());

            reader.MoveToElement();
        }

        if (reader.IsEmptyElement)
        {
            return;
        }

        // 2. Из прямых детей берем только leaf-элементы; nested XML flat provider не раскрывает.
        while (await ReadAsync(reader, cancellationToken).ConfigureAwait(false))
        {
            if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == rowDepth)
            {
                return;
            }

            if (reader.NodeType != XmlNodeType.Element || reader.Depth != rowDepth + 1)
            {
                continue;
            }

            var name = reader.LocalName;
            if (await IsLeafElementAsync(reader, cancellationToken).ConfigureAwait(false))
            {
                AddColumn(columns, paths, name, name);
            }
        }
    }

    private static async ValueTask<bool> IsLeafElementAsync(
        XmlReader reader,
        CancellationToken cancellationToken)
    {
        if (reader.IsEmptyElement)
        {
            return true;
        }

        var elementDepth = reader.Depth;
        var isLeaf = true;

        // 1. Доходим до закрытия элемента, не сохраняя его текстовое содержимое.
        while (await ReadAsync(reader, cancellationToken).ConfigureAwait(false))
        {
            if (reader.NodeType == XmlNodeType.Element && reader.Depth > elementDepth)
            {
                isLeaf = false;
            }

            if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == elementDepth)
            {
                return isLeaf;
            }
        }

        throw new XmlException("Unexpected end of XML while analyzing a table row.");
    }

    private static void AddColumn(
        List<XmlColumnSchema> columns,
        HashSet<string> paths,
        string name,
        string path)
    {
        if (!paths.Add(path))
        {
            return;
        }

        columns.Add(new XmlColumnSchema { Name = name, Path = path });
    }

    private static bool IsNamespaceDeclaration(XmlReader reader)
    {
        return reader.Prefix == "xmlns" || reader.Name == "xmlns";
    }

    private static async ValueTask<bool> ReadAsync(XmlReader reader, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await reader.ReadAsync().ConfigureAwait(false);
    }
}

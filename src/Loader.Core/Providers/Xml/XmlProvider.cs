using System.Data.Common;
using Loader.Core.Abstractions;
using Loader.Core.Exceptions;
using Loader.Core.Sources;

namespace Loader.Core.Providers.Xml;

/// <summary>
/// Потоковый provider плоских XML-таблиц в форме, используемой PIX BI:
/// выбранные по имени элементы становятся строками, их атрибуты и прямые leaf-элементы — колонками.
/// Provider не строит <c>DataSet</c>, не загружает документ целиком и возвращает все значения строками.
/// Для чтения требуется заранее известная схема; ее можно получить отдельным полным проходом
/// через <see cref="AnalyzeSchemaAsync"/>.
/// </summary>
public sealed class XmlProvider : IProvider<IFileSource, XmlTableConfig>
{
    public string Kind => "xml";

    public async ValueTask<DbDataReader> OpenReaderAsync(
        IFileSource source,
        XmlTableConfig config,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Проверяем однозначность flat-схемы до чтения файла.
            XmlTableSchemaValidator.Validate(config.FileName, config.Schema);

            // 2. Открываем поток и заранее ищем первую строку, чтобы HasRows был точным.
            var stream = source.OpenRead(config.FileName);
            return await XmlProviderDataReader
                .CreateAsync(stream, config.FileName, config.TableName, config.Schema, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException
                                   and not XmlTableNotFoundProviderException
                                   and not XmlInvalidSchemaProviderException)
        {
            throw new XmlFileOpenProviderException(config.FileName, ex);
        }
    }

    public async ValueTask<XmlTableSchema> AnalyzeSchemaAsync(
        IFileSource source,
        string fileName,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var stream = source.OpenRead(fileName);
            return await XmlSchemaStreamAnalyzer
                .AnalyzeAsync(stream, fileName, tableName, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException
                                   and not XmlTableNotFoundProviderException
                                   and not XmlInvalidSchemaProviderException)
        {
            throw new XmlFileOpenProviderException(fileName, ex);
        }
    }
}

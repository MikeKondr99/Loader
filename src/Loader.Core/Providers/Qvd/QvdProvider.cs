using System.Data.Common;
using Loader.Core.Abstractions;
using Loader.Core.Sources;

namespace Loader.Core.Providers.Qvd;

/// <summary>
/// Provider потокового чтения QVD-файлов.
/// QVD хранит значения через таблицы символов: provider сначала предзагружает symbol tables в память,
/// по ним выводит стабильные CLR-типы колонок, а затем потоково читает row section без материализации всех строк.
/// Это значит, что потребление памяти зависит от количества уникальных значений в колонках, а не от количества строк.
/// </summary>
public sealed class QvdProvider : IProvider<IFileSource, QvdTableConfig>
{
    public string Kind => "qvd";

    public async ValueTask<DbDataReader> OpenReaderAsync(
        IFileSource source,
        QvdTableConfig config,
        CancellationToken cancellationToken = default)
    {
        Stream stream;
        try
        {
            stream = source.OpenRead(config.FileName);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new QvdFileOpenProviderException(config.FileName, ex);
        }

        try
        {
            if (!stream.CanSeek)
            {
                throw new QvdFormatProviderException(
                    config.FileName,
                    "QVD reader requires a seekable stream because symbol tables and rows are stored in different file sections.");
            }

            // 1. Читаем XML-заголовок и определяем начало бинарной секции.
            var header = await QvdHeaderReader
                .ReadAsync(stream, config.FileName, cancellationToken)
                .ConfigureAwait(false);

            // 2. Проверяем границы секций до чтения бинарных данных.
            QvdLayoutValidator.Validate(stream.Length, header.BinarySectionOffset, header.Table);

            // 3. Предзагружаем таблицы символов: без этого нельзя стабильно вывести схему DbDataReader.
            var symbolsByField = await QvdSymbolTableReader
                .ReadAsync(stream, header.BinarySectionOffset, header.Table, cancellationToken)
                .ConfigureAwait(false);

            // 4. Возвращаем reader, который дальше читает только row section батчами.
            return new QvdProviderDataReader(stream, header, symbolsByField);
        }
        catch (QvdFormatProviderException)
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            throw new QvdFormatProviderException(config.FileName, "Unexpected QVD format or IO error.", ex);
        }
    }
}

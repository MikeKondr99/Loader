using System.Buffers;

namespace Loader.Core.Providers.Qvd;

internal static class QvdSymbolTableReader
{
    public static async Task<object?[][]> ReadAsync(
        Stream stream,
        int binarySectionOffset,
        QvdTableHeader tableHeader,
        CancellationToken cancellationToken)
    {
        var symbolsByField = new object?[tableHeader.Fields.Count][];
        var fieldRanges = tableHeader.Fields
            .Select((field, index) => new QvdFieldRange(index, field))
            .OrderBy(static range => range.Field.Offset)
            .ToArray();

        foreach (var fieldRange in fieldRanges)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var field = fieldRange.Field;
            stream.Seek(binarySectionOffset + field.Offset, SeekOrigin.Begin);

            var rentedBuffer = ArrayPool<byte>.Shared.Rent(field.Length);
            try
            {
                var fieldBuffer = rentedBuffer.AsMemory(0, field.Length);
                await FillBufferAsync(stream, fieldBuffer, cancellationToken).ConfigureAwait(false);
                symbolsByField[fieldRange.Index] = QvdSymbolDecoder.Decode(fieldBuffer.Span, field);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }

        return symbolsByField;
    }

    private static async Task FillBufferAsync(
        Stream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[totalRead..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("Unexpected end of QVD symbol section.");
            }

            totalRead += read;
        }
    }

    private readonly record struct QvdFieldRange(int Index, QvdFieldHeader Field);
}

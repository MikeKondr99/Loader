using System.Buffers;
using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace Loader.Core.Providers.Qvd;

internal static class QvdHeaderReader
{
    private const int ReadBufferBytes = 16 * 1024;
    private const int MaxXmlHeaderBytes = 16 * 1024 * 1024;
    private static readonly byte[] EndHeaderBytes = Encoding.UTF8.GetBytes("</QvdTableHeader>");

    public static async Task<QvdHeader> ReadAsync(
        Stream stream,
        string fileName,
        CancellationToken cancellationToken)
    {
        var (xmlHeader, binarySectionOffset) = await ReadXmlHeaderAsync(stream, cancellationToken)
            .ConfigureAwait(false);

        return new QvdHeader(
            fileName,
            binarySectionOffset,
            ParseTableHeader(xmlHeader));
    }

    private static async Task<(string XmlHeader, int BinarySectionOffset)> ReadXmlHeaderAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        stream.Seek(0, SeekOrigin.Begin);

        using var bufferStream = new MemoryStream(capacity: ReadBufferBytes);
        var buffer = ArrayPool<byte>.Shared.Rent(ReadBufferBytes);
        try
        {
            var matchedBytes = 0;
            var endTagFound = false;
            var trailingNewLineBytesToCapture = 0;
            var totalBuffered = 0;
            var totalConsumed = 0;

            while (true)
            {
                var read = await stream
                    .ReadAsync(buffer.AsMemory(0, ReadBufferBytes), cancellationToken)
                    .ConfigureAwait(false);

                if (read == 0)
                {
                    throw new InvalidDataException("Could not find QVD XML table header.");
                }

                totalBuffered = checked(totalBuffered + read);
                if (totalBuffered > MaxXmlHeaderBytes)
                {
                    throw new InvalidDataException($"QVD XML table header exceeds {MaxXmlHeaderBytes} bytes.");
                }

                bufferStream.Write(buffer, 0, read);

                for (var i = 0; i < read; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var nextByte = buffer[i];
                    if (!endTagFound)
                    {
                        matchedBytes = nextByte == EndHeaderBytes[matchedBytes]
                            ? matchedBytes + 1
                            : nextByte == EndHeaderBytes[0] ? 1 : 0;

                        if (matchedBytes == EndHeaderBytes.Length)
                        {
                            endTagFound = true;
                            trailingNewLineBytesToCapture = 2;
                        }

                        continue;
                    }

                    if (trailingNewLineBytesToCapture > 0 &&
                        (nextByte == (byte)'\r' || nextByte == (byte)'\n'))
                    {
                        trailingNewLineBytesToCapture--;
                        continue;
                    }

                    if (nextByte == 0)
                    {
                        var xmlLength = checked(totalConsumed + i);
                        var xml = Encoding.UTF8.GetString(bufferStream.GetBuffer(), 0, xmlLength);
                        return (xml, xmlLength + 1);
                    }

                    throw new InvalidDataException("Could not find QVD XML table header terminator.");
                }

                totalConsumed = checked(totalConsumed + read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static QvdTableHeader ParseTableHeader(string xmlHeader)
    {
        var root = XDocument.Parse(xmlHeader).Root
            ?? throw new InvalidDataException("QVD XML table header is empty.");

        var fields = root
            .Elements()
            .FirstOrDefault(static element => element.Name.LocalName == "Fields")
            ?.Elements()
            .Where(static element => element.Name.LocalName == "QvdFieldHeader")
            .Select(ParseField)
            .ToArray() ?? [];

        return new QvdTableHeader
        {
            TableName = StringValue(root, "TableName") ?? string.Empty,
            Fields = fields,
            NoOfRecords = IntValue(root, "NoOfRecords"),
            RecordByteSize = IntValue(root, "RecordByteSize"),
            Offset = IntValue(root, "Offset"),
            Length = IntValue(root, "Length")
        };
    }

    private static QvdFieldHeader ParseField(XElement element)
    {
        var numberFormat = element
            .Elements()
            .FirstOrDefault(static child => child.Name.LocalName == "NumberFormat");

        return new QvdFieldHeader
        {
            FieldName = StringValue(element, "FieldName") ?? string.Empty,
            Offset = IntValue(element, "Offset"),
            Length = IntValue(element, "Length"),
            BitOffset = IntValue(element, "BitOffset"),
            BitWidth = IntValue(element, "BitWidth"),
            Bias = IntValue(element, "Bias"),
            NumberFormatType = numberFormat is null ? null : StringValue(numberFormat, "Type")
        };
    }

    private static string? StringValue(XElement element, string name)
    {
        return element
            .Elements()
            .FirstOrDefault(child => child.Name.LocalName == name)
            ?.Value;
    }

    private static int IntValue(XElement element, string name)
    {
        var value = StringValue(element, name);
        return string.IsNullOrWhiteSpace(value) ? 0 : int.Parse(value, CultureInfo.InvariantCulture);
    }
}

namespace Loader.Core.Providers.Qvd;

internal static class QvdLayoutValidator
{
    public static void Validate(long fileLength, int binarySectionOffset, QvdTableHeader tableHeader)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(binarySectionOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(tableHeader.NoOfRecords);
        ArgumentOutOfRangeException.ThrowIfNegative(tableHeader.RecordByteSize);
        ArgumentOutOfRangeException.ThrowIfNegative(tableHeader.Offset);

        if (tableHeader is { NoOfRecords: > 0, RecordByteSize: 0 })
        {
            throw new InvalidDataException("QVD row section has records with zero byte size.");
        }

        if (binarySectionOffset > fileLength)
        {
            throw new InvalidDataException("QVD binary section offset exceeds the file length.");
        }

        var rowSectionStart = checked(binarySectionOffset + (long)tableHeader.Offset);
        var rowSectionLength = checked((long)tableHeader.NoOfRecords * tableHeader.RecordByteSize);
        var rowSectionEnd = checked(rowSectionStart + rowSectionLength);
        if (rowSectionEnd > fileLength)
        {
            throw new InvalidDataException("QVD row section exceeds the file length.");
        }

        foreach (var field in tableHeader.Fields)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(field.Offset);
            ArgumentOutOfRangeException.ThrowIfNegative(field.Length);
            ArgumentOutOfRangeException.ThrowIfNegative(field.BitOffset);
            ArgumentOutOfRangeException.ThrowIfNegative(field.BitWidth);

            var lastBitExclusive = checked((long)field.BitOffset + field.BitWidth);
            var rowBits = checked((long)tableHeader.RecordByteSize * 8);
            if (lastBitExclusive > rowBits)
            {
                throw new InvalidDataException($"QVD field '{field.FieldName}' points outside the row record.");
            }

            var fieldStart = checked(binarySectionOffset + (long)field.Offset);
            var fieldEnd = checked(fieldStart + field.Length);
            if (fieldStart > fileLength || fieldEnd > rowSectionStart)
            {
                throw new InvalidDataException($"QVD symbol section for field '{field.FieldName}' is outside the file bounds.");
            }
        }
    }

    public static void EnsureAvailableBytes(int end, int start, int bytesToRead, string tokenType)
    {
        if (start < 0 || start > end - bytesToRead)
        {
            throw new InvalidDataException($"QVD symbol section ended before a complete {tokenType} token was read.");
        }
    }
}

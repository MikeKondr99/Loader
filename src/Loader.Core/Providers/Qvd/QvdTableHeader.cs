namespace Loader.Core.Providers.Qvd;

internal sealed record QvdTableHeader
{
    public required string TableName { get; init; }

    public required IReadOnlyList<QvdFieldHeader> Fields { get; init; }

    public required int NoOfRecords { get; init; }

    public required int RecordByteSize { get; init; }

    public required int Offset { get; init; }

    public required int Length { get; init; }
}

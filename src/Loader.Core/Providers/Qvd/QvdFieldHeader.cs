namespace Loader.Core.Providers.Qvd;

internal sealed record QvdFieldHeader
{
    public required string FieldName { get; init; }

    public required int Offset { get; init; }

    public required int Length { get; init; }

    public required int BitOffset { get; init; }

    public required int BitWidth { get; init; }

    public required int Bias { get; init; }

    public required string? NumberFormatType { get; init; }
}

namespace Loader.Core.Providers.Qvd;

internal sealed record QvdHeader(
    string FileName,
    int BinarySectionOffset,
    QvdTableHeader Table);

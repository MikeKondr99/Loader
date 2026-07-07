using System.Text;
using Loader.Core.Sources;

namespace Loader.Core.Tests.Infrastructure;

internal sealed class InlineCsv : IFileSource
{
    private readonly string _content;
    private readonly Encoding _sourceEncoding;

    public InlineCsv(string content, Encoding? sourceEncoding = null)
    {
        _content = content;
        _sourceEncoding = sourceEncoding ?? Encoding.UTF8;
    }

    public TextReader OpenText(string fileName, Encoding? encoding = null)
    {
        var bytes = _sourceEncoding.GetBytes(_content);
        var stream = new MemoryStream(bytes);
        var readerEncoding = encoding ?? Encoding.UTF8;

        return new StreamReader(stream, readerEncoding, detectEncodingFromByteOrderMarks: true);
    }
}

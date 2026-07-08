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

    public Stream OpenRead(string fileName)
    {
        var bytes = _sourceEncoding.GetBytes(_content);
        return new MemoryStream(bytes);
    }
}

using System.Text;
using Loader.Core.Sources;

namespace Loader.Core.Tests.Infrastructure;

/// <summary>
/// Тестовый file source, который отдает JSON из строки независимо от имени файла.
/// </summary>
internal sealed class InlineJson : IFileSource
{
    private readonly string _content;
    private readonly Encoding _sourceEncoding;

    public InlineJson(string content, Encoding? sourceEncoding = null)
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

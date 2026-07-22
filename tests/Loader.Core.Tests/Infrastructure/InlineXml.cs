using System.Text;
using Loader.Core.Sources;

namespace Loader.Core.Tests.Infrastructure;

/// <summary>
/// Тестовый file source, который отдает XML из строки независимо от имени файла.
/// </summary>
internal sealed class InlineXml : IFileSource
{
    private readonly string _content;
    private readonly Encoding _sourceEncoding;

    public InlineXml(string content, Encoding? sourceEncoding = null)
    {
        _content = content;
        _sourceEncoding = sourceEncoding ?? Encoding.UTF8;
    }

    public Stream OpenRead(string fileName)
    {
        return new MemoryStream(_sourceEncoding.GetBytes(_content));
    }
}

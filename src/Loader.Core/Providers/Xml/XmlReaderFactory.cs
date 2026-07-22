using System.Xml;

namespace Loader.Core.Providers.Xml;

internal static class XmlReaderFactory
{
    public static XmlReader Create(Stream stream)
    {
        return XmlReader.Create(
            stream,
            new XmlReaderSettings
            {
                Async = true,
                CloseInput = true,
                DtdProcessing = DtdProcessing.Prohibit,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                XmlResolver = null
            });
    }
}

using System.Text;
using Loader.Core.Sources;

namespace Loader.Core.Tests;

public sealed class FileSystemSourceTests
{
    [Test]
    [DisplayName("FileSystemSource читает файл, который открыт другим процессом на запись")]
    public async Task OpenRead_allows_reading_file_opened_for_write()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "Loader.Core.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var filePath = Path.Combine(tempDirectory, "opened.csv");
            await File.WriteAllTextAsync(filePath, "id,name\r\n1,Alice", Encoding.UTF8);

            await using var lockStream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.ReadWrite);
            var source = new FileSystemSource(tempDirectory);

            await using var readStream = source.OpenRead("opened.csv");
            using var reader = new StreamReader(readStream, Encoding.UTF8);

            await Assert.That(await reader.ReadToEndAsync()).IsEqualTo("id,name\r\n1,Alice");
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }
}

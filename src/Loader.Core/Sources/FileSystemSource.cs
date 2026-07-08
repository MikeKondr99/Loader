using Loader.Core.Abstractions;

namespace Loader.Core.Sources;

/// <summary>
/// Source, который умеет открыть файл по имени как бинарный поток.
/// </summary>
public interface IFileSource : ISource
{
    Stream OpenRead(string fileName);
}

/// <summary>
/// Source, который умеет открыть файл по имени и выдать его физический путь.
/// </summary>
public interface IPhysicalFileSource : IFileSource
{
    string ResolveFilePath(string fileName);
}

/// <summary>
/// Source файловой системы с безопасным разрешением относительных путей внутри root path.
/// </summary>
public sealed class FileSystemSource : IPhysicalFileSource
{
    private readonly string _fullRootPath;

    public FileSystemSource(string rootPath)
    {
        if (rootPath.Trim().Length == 0)
        {
            throw new ArgumentException("Root path must not be empty.", nameof(rootPath));
        }

        RootPath = rootPath;
        _fullRootPath = NormalizeRootPath(rootPath);
    }

    public string RootPath { get; }

    public Stream OpenRead(string fileName)
    {
        var fullPath = ResolveFilePath(fileName);
        return File.OpenRead(fullPath);
    }

    public string ResolveFilePath(string fileName)
    {
        if (fileName.Trim().Length == 0)
        {
            throw new ArgumentException("File name must not be empty.", nameof(fileName));
        }

        if (Path.IsPathRooted(fileName))
        {
            throw new ArgumentException("File name must be relative to the source root path.", nameof(fileName));
        }

        var fullPath = Path.GetFullPath(Path.Combine(_fullRootPath, fileName));
        if (!fullPath.StartsWith(_fullRootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("File name must stay inside the source root path.", nameof(fileName));
        }

        return fullPath;
    }

    private static string NormalizeRootPath(string rootPath)
    {
        var fullPath = Path.GetFullPath(rootPath);

        if (fullPath.EndsWith(Path.DirectorySeparatorChar) ||
            fullPath.EndsWith(Path.AltDirectorySeparatorChar))
        {
            return fullPath;
        }

        return fullPath + Path.DirectorySeparatorChar;
    }
}

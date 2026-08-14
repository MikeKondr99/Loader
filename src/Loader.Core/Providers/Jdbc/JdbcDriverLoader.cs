namespace Loader.Core.Providers.Jdbc;

internal static class JdbcDriverLoader
{
    public static java.net.URLClassLoader Create(IReadOnlyList<string> jarPaths)
    {
        if (jarPaths.Count == 0)
        {
            throw new JdbcProviderException("JDBC connection string должен содержать хотя бы один путь в 'JarPath'.");
        }

        var paths = ExpandJarPaths(jarPaths);
        var urls = new java.net.URL[paths.Count];
        for (var index = 0; index < paths.Count; index++)
        {
            var path = paths[index];
            urls[index] = new java.io.File(path).toURI().toURL();
        }

        return new java.net.URLClassLoader(urls, java.lang.Thread.currentThread().getContextClassLoader());
    }

    public static java.sql.Driver CreateDriver(java.lang.ClassLoader loader, string driverClass)
    {
        var type = java.lang.Class.forName(driverClass, true, loader);
        var constructor = type.getDeclaredConstructor([]);
        return (java.sql.Driver)constructor.newInstance([]);
    }

    private static IReadOnlyList<string> ExpandJarPaths(IReadOnlyList<string> jarPaths)
    {
        var result = new List<string>();
        foreach (var item in jarPaths)
        {
            var path = Path.GetFullPath(item);
            if (Directory.Exists(path))
            {
                result.AddRange(Directory.EnumerateFiles(path, "*.jar", SearchOption.AllDirectories));
                continue;
            }

            if (!File.Exists(path))
            {
                throw new JdbcProviderException($"JDBC jar или директория '{path}' не найдены.");
            }

            result.Add(path);
        }

        if (result.Count == 0)
        {
            throw new JdbcProviderException("JDBC JarPath не содержит jar-файлов.");
        }

        return result;
    }
}

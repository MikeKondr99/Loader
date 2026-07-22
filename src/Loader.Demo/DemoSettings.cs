using System.Text.Json;

namespace Loader.Demo;

internal sealed record DemoSettings
{
    public required ClickHouseSettings ClickHouse { get; init; }

    public static DemoSettings Load()
    {
        var path = ResolveSettingsPath();
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<DemoSettings>(
                   json,
                   new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? throw new InvalidOperationException($"Файл настроек '{path}' пуст.");
    }

    private static string ResolveSettingsPath()
    {
        var currentDirectoryPath = Path.Combine(Environment.CurrentDirectory, "appsettings.json");
        if (File.Exists(currentDirectoryPath))
        {
            return currentDirectoryPath;
        }

        var executablePath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (File.Exists(executablePath))
        {
            return executablePath;
        }

        throw new FileNotFoundException("Файл appsettings.json не найден.");
    }
}

internal sealed record ClickHouseSettings
{
    public required string ConnectionString { get; init; }

    public string? Database { get; init; }

    public string TablePrefix { get; init; } = "loader_demo_";
}

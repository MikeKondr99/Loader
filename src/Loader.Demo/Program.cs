using Loader.Demo;

var log = new DemoLog();
if (args.Length != 1)
{
    log.Error("Использование: loader <файл-скрипта>");
    return 1;
}

try
{
    var scriptPath = Path.GetFullPath(args[0]);
    var settingsOperation = log.Begin("Читаю настройки");
    var settings = DemoSettings.Load();
    settingsOperation.Complete("Настройки прочитаны");
    await new DemoRunner(settings, log).RunAsync(scriptPath).ConfigureAwait(false);
    return 0;
}
catch (Exception ex)
{
    log.Error($"Ошибка: {ex.Message}");
    return 1;
}

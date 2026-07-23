using System.Text.RegularExpressions;
using ClickHouse.Client.ADO;
using Loader.Core.Decorators;
using Loader.Core.Models;
using Loader.Core.Providers.ClickHouse;
using Loader.Core.Providers.Sql;
using Loader.Core.Sources;
using Loader.Core.Writers.ClickHouse;
using Loader.Lang.Statements;

namespace Loader.Demo;

internal sealed partial class DemoRunner
{
    private const long ProgressInterval = 100_000;
    private const int DebugExportLimit = 1_000;

    private readonly ClickHouseSettings _settings;
    private readonly DemoLog _log;
    private readonly ConnectionStringSource _clickHouseSource;
    private readonly ClickHouseWriter _writer = new();
    private readonly SylvanCsvExporter _csvExporter = new();

    public DemoRunner(DemoSettings settings, DemoLog log)
    {
        _settings = settings.ClickHouse;
        _log = log;
        if (!SafePrefixRegex().IsMatch(_settings.TablePrefix))
        {
            throw new InvalidOperationException(
                $"Префикс таблиц ClickHouse '{_settings.TablePrefix}' может содержать только буквы, цифры и подчеркивания.");
        }

        _clickHouseSource = new ConnectionStringSource
        {
            ConnectionString = _settings.ConnectionString
        };
    }

    public async Task RunAsync(string scriptPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException($"Файл скрипта '{scriptPath}' не найден.", scriptPath);
        }

        // 1. Читаем и парсим ровно один LOAD statement из переданного файла.
        var readScriptOperation = _log.Begin("Читаю скрипт");
        var script = await File.ReadAllTextAsync(scriptPath, cancellationToken).ConfigureAwait(false);
        readScriptOperation.Complete("Скрипт прочитан");

        var parseOperation = _log.Begin("Разбираю скрипт");
        var parseResult = Statement.Parse(script);
        if (!parseResult.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Не удалось разобрать скрипт в позиции {parseResult.Error.Span}: {parseResult.Error.Message}");
        }

        if (parseResult.Value is not LoadStatement load)
        {
            throw new InvalidOperationException("Loader.Demo поддерживает только один LOAD statement.");
        }
        parseOperation.Complete("Скрипт разобран");

        var scriptDirectory = Path.GetDirectoryName(scriptPath) ?? Environment.CurrentDirectory;
        var resolveSourceOperation = _log.Begin("Определяю источник данных");
        var source = await DemoSourceResolver
            .ResolveAsync(load, scriptDirectory, cancellationToken)
            .ConfigureAwait(false);
        resolveSourceOperation.Complete($"Источник данных определен: {source.Kind}");
        var suffix = Guid.NewGuid().ToString("N");
        var stageTable = Table($"{_settings.TablePrefix}stage_{suffix}");
        var finalTable = Table($"{_settings.TablePrefix}result_{suffix}");
        var stageCreated = false;

        try
        {
            // 2. Provider reader получает физические columnN и потоково сохраняется в staging ClickHouse.
            var stagingOperation = _log.Begin($"Загружаю данные во временную таблицу {stageTable.ToSql()}");
            var rawReader = await source.OpenReaderAsync(cancellationToken).ConfigureAwait(false);
            var physicalReader = new PhysicalColumnDataReader(rawReader);
            var originalNames = physicalReader.OriginalNames.ToArray();
            await using var normalizedStageReader = physicalReader.Normalize(
                new NormalizeOptions { Buffer = source.RequiresBuffer });
            await using var stageReader = new ObservedDomainDataReader(
                normalizedStageReader,
                _log,
                "Загрузка во временную таблицу",
                ProgressInterval);
            var stageSchema = stageReader.DataSchema;

            stageCreated = true;
            try
            {
                await _writer.WriteAsync(
                    _clickHouseSource,
                    stageReader,
                    new ClickHouseWriteOptions { TableName = stageTable },
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                var memory = stageReader.CaptureMemory();
                _log.Error(
                    $"Загрузка во временную таблицу остановилась. Строк прочитано: {stageReader.RowsRead:N0}. Память: {memory.ToLogString()}");
                throw;
            }

            var stageMemory = stageReader.CaptureMemory();
            stagingOperation.Complete(
                $"Данные загружены во временную таблицу. Строк: {stageReader.RowsRead:N0}. Память: {stageMemory.ToLogString()}");

            // 3. LOAD fields становятся SELECT над staging, а его reader записывается в final table.
            var loadOperation = _log.Begin("Применяю LOAD и сохраняю итоговую таблицу");
            var query = LoadSelectCompiler.Compile(load, originalNames, stageSchema, stageTable.ToSql());
            await using var queryRawReader = await new ClickHouseProvider().OpenReaderAsync(
                _clickHouseSource,
                new SqlTableConfig { Sql = query.Sql },
                cancellationToken).ConfigureAwait(false);
            await using var normalizedFinalReader = queryRawReader.Normalize();
            await using var finalReader = new ObservedDomainDataReader(
                normalizedFinalReader,
                _log,
                "Загрузка в итоговую таблицу",
                ProgressInterval);
            try
            {
                await _writer.WriteAsync(
                    _clickHouseSource,
                    finalReader,
                    new ClickHouseWriteOptions { TableName = finalTable },
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                var memory = finalReader.CaptureMemory();
                _log.Error(
                    $"Загрузка в итоговую таблицу остановилась. Строк прочитано: {finalReader.RowsRead:N0}. Память: {memory.ToLogString()}");
                throw;
            }

            var finalMemory = finalReader.CaptureMemory();
            loadOperation.Complete(
                $"LOAD применен, итоговая таблица сохранена. Строк: {finalReader.RowsRead:N0}. Память: {finalMemory.ToLogString()}");

            _log.Info($"Итоговая таблица: {finalTable.ToSql()}");
            for (var ordinal = 0; ordinal < query.LogicalNames.Count; ordinal++)
            {
                var field = finalReader.DataSchema.Fields[ordinal];
                _log.Info(
                    $"column{ordinal + 1} = {query.LogicalNames[ordinal]}, " +
                    $"{field.DataType} ({field.ClrType.Name})");
            }

            // 4. Повторно читаем final table и потоково записываем ее в CSV рядом со скриптом.
            var csvPath = Path.Combine(
                Path.GetDirectoryName(scriptPath) ?? Environment.CurrentDirectory,
                $"{Path.GetFileNameWithoutExtension(scriptPath)}.result.csv");
            var csvOperation = _log.Begin($"Экспортирую первые {DebugExportLimit:N0} строк итоговой таблицы в CSV '{csvPath}'");
            await ExportCsvAsync(finalTable, csvPath, cancellationToken).ConfigureAwait(false);
            csvOperation.Complete("Первые строки итоговой таблицы экспортированы в CSV");
        }
        finally
        {
            // 5. Staging является технической таблицей и удаляется без участия LOAD/query abstractions.
            if (stageCreated)
            {
                var cleanupOperation = _log.Begin($"Удаляю временную таблицу {stageTable.ToSql()}");
                await DropTableAsync(stageTable, CancellationToken.None).ConfigureAwait(false);
                cleanupOperation.Complete("Временная таблица удалена");
            }
        }
    }

    private ClickHouseTableName Table(string name)
    {
        return new ClickHouseTableName
        {
            Database = string.IsNullOrWhiteSpace(_settings.Database) ? null : _settings.Database,
            Table = name
        };
    }

    private async Task DropTableAsync(ClickHouseTableName table, CancellationToken cancellationToken)
    {
        await using var connection = new ClickHouseConnection(_settings.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE IF EXISTS {table.ToSql()}";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ExportCsvAsync(
        ClickHouseTableName table,
        string outputPath,
        CancellationToken cancellationToken)
    {
        await using var rawReader = await new ClickHouseProvider().OpenReaderAsync(
            _clickHouseSource,
            new SqlTableConfig { Sql = $"SELECT * FROM {table.ToSql()} LIMIT {DebugExportLimit}" },
            cancellationToken).ConfigureAwait(false);
        await using var reader = rawReader.Normalize();
        await using var output = File.Create(outputPath);
        await _csvExporter.ExportAsync(reader, output, cancellationToken).ConfigureAwait(false);
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SafePrefixRegex();
}

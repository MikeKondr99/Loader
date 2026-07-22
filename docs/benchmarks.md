# Benchmarks

## Что есть

- `ReaderPipelineBenchmarks` - синтетический `DbDataReader`, проверяет overhead `Normalize`, `Where`, `AutoCast`, `CollectMeta`.
- `CsvPipelineBenchmarks` - CSV provider без ClickHouse, проверяет чтение CSV + pipeline.
- `WideClickHouseLoadBenchmarks` - end-to-end перегрузка 1 000 000 строк в ClickHouse.

Список benchmark-методов:

```powershell
dotnet run -c Release --project benchmarks\Loader.Benchmarks -- --list flat
```

## Текущий Docker Setup

Сейчас используется контейнер:

```text
loader-bench-clickhouse
image: clickhouse/clickhouse-server:latest
ports: 8123, 9000
database: loader_bench
user: loader
password: loader
```

Проверить контейнер:

```powershell
docker ps --format "table {{.Names}}\t{{.Image}}\t{{.Ports}}"
docker exec loader-bench-clickhouse clickhouse-client --user loader --password loader --database loader_bench --query "SELECT 1"
```

Если контейнера нет:

```powershell
docker run -d --name loader-bench-clickhouse `
  -p 8123:8123 -p 9000:9000 `
  -e CLICKHOUSE_DB=loader_bench `
  -e CLICKHOUSE_USER=loader `
  -e CLICKHOUSE_PASSWORD=loader `
  clickhouse/clickhouse-server:latest
```

## Быстрые Локальные Benchmarks

Без Docker и внешних БД:

```powershell
dotnet run -c Release --project benchmarks\Loader.Benchmarks -- --filter "*ReaderPipelineBenchmarks*"
dotnet run -c Release --project benchmarks\Loader.Benchmarks -- --filter "*CsvPipelineBenchmarks*"
```

Один конкретный метод:

```powershell
dotnet run -c Release --project benchmarks\Loader.Benchmarks -- --filter "*Csv_provider_normalize_autocast*"
```

## Wide Load В ClickHouse

Базовая строка подключения под текущий контейнер:

```powershell
$env:LOADER_BENCH_CLICKHOUSE_TARGET = "Host=localhost;Port=8123;Protocol=http;Database=loader_bench;Username=loader;Password=loader"
```

CSV -> ClickHouse:

```powershell
$env:LOADER_BENCH_CLICKHOUSE_TARGET = "Host=localhost;Port=8123;Protocol=http;Database=loader_bench;Username=loader;Password=loader"
dotnet run -c Release --project benchmarks\Loader.Benchmarks -- --filter "*WideClickHouseLoadBenchmarks.Csv_to_clickhouse"
```

JSON -> ClickHouse с анализом схемы:

```powershell
$env:LOADER_BENCH_CLICKHOUSE_TARGET = "Host=localhost;Port=8123;Protocol=http;Database=loader_bench;Username=loader;Password=loader"
dotnet run -c Release --project benchmarks\Loader.Benchmarks -- --filter "*WideClickHouseLoadBenchmarks.Json_to_clickhouse_with_schema_analyze"
```

XML -> ClickHouse с анализом схемы:

```powershell
$env:LOADER_BENCH_CLICKHOUSE_TARGET = "Host=localhost;Port=8123;Protocol=http;Database=loader_bench;Username=loader;Password=loader"
dotnet run -c Release --project benchmarks\Loader.Benchmarks -- --filter "*WideClickHouseLoadBenchmarks.Xml_to_clickhouse_with_schema_analyze"
```

Excel -> ClickHouse:

```powershell
$env:LOADER_BENCH_CLICKHOUSE_TARGET = "Host=localhost;Port=8123;Protocol=http;Database=loader_bench;Username=loader;Password=loader"
dotnet run -c Release --project benchmarks\Loader.Benchmarks -- --filter "*WideClickHouseLoadBenchmarks.Excel_to_clickhouse"
```

ClickHouse -> ClickHouse через тот же контейнер:

```powershell
$env:LOADER_BENCH_CLICKHOUSE_TARGET = "Host=localhost;Port=8123;Protocol=http;Database=loader_bench;Username=loader;Password=loader"
$env:LOADER_BENCH_CLICKHOUSE_SOURCE = $env:LOADER_BENCH_CLICKHOUSE_TARGET
dotnet run -c Release --project benchmarks\Loader.Benchmarks -- --filter "*WideClickHouseLoadBenchmarks.ClickHouse_to_clickhouse"
```

CSV + JSON + XML + Excel + ClickHouse одним запуском:

```powershell
$env:LOADER_BENCH_CLICKHOUSE_TARGET = "Host=localhost;Port=8123;Protocol=http;Database=loader_bench;Username=loader;Password=loader"
$env:LOADER_BENCH_CLICKHOUSE_SOURCE = $env:LOADER_BENCH_CLICKHOUSE_TARGET
dotnet run -c Release --project benchmarks\Loader.Benchmarks -- --filter `
  "*WideClickHouseLoadBenchmarks.Csv_to_clickhouse" `
  "*WideClickHouseLoadBenchmarks.Json_to_clickhouse_with_schema_analyze" `
  "*WideClickHouseLoadBenchmarks.Xml_to_clickhouse_with_schema_analyze" `
  "*WideClickHouseLoadBenchmarks.Excel_to_clickhouse" `
  "*WideClickHouseLoadBenchmarks.ClickHouse_to_clickhouse"
```

## Peak Memory

BenchmarkDotNet `Allocated` показывает суммарные managed allocations за запуск, а не максимальную память процесса.

Для печати peak memory внутри wide load:

```powershell
$env:LOADER_BENCH_CLICKHOUSE_TARGET = "Host=localhost;Port=8123;Protocol=http;Database=loader_bench;Username=loader;Password=loader"
$env:LOADER_BENCH_PRINT_PEAK_MEMORY = "1"
dotnet run -c Release --project benchmarks\Loader.Benchmarks -- --filter "*WideClickHouseLoadBenchmarks.Csv_to_clickhouse"
```

В выводе искать строки вида:

```text
Csv_to_clickhouse peak: working_set=..., private=..., managed_heap=...
```

После запуска можно сбросить флаг:

```powershell
Remove-Item Env:\LOADER_BENCH_PRINT_PEAK_MEMORY
```

## Postgres И SQL Server

Эти методы требуют отдельные env:

```powershell
$env:LOADER_BENCH_POSTGRES = "<postgres connection string>"
$env:LOADER_BENCH_SQLSERVER = "<sql server connection string>"
$env:LOADER_BENCH_CLICKHOUSE_TARGET = "Host=localhost;Port=8123;Protocol=http;Database=loader_bench;Username=loader;Password=loader"
```

Postgres -> ClickHouse:

```powershell
dotnet run -c Release --project benchmarks\Loader.Benchmarks -- --filter "*WideClickHouseLoadBenchmarks.Postgres_to_clickhouse"
```

SQL Server -> ClickHouse:

```powershell
dotnet run -c Release --project benchmarks\Loader.Benchmarks -- --filter "*WideClickHouseLoadBenchmarks.SqlServer_to_clickhouse"
```

Все wide benchmarks:

```powershell
dotnet run -c Release --project benchmarks\Loader.Benchmarks -- --filter "*WideClickHouseLoadBenchmarks*"
```

## Где Лежат Результаты

BenchmarkDotNet пишет отчеты в:

```text
benchmarks/Loader.Benchmarks/BenchmarkDotNet.Artifacts/results
```

Первый запуск `WideClickHouseLoadBenchmarks` генерирует fixture-файлы для CSV/JSON/XML/Excel в папке benchmark-проекта. Это может занять заметное время, следующие запуски используют уже созданные файлы.

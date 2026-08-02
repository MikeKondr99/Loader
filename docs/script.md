# Script

`Loader.Demo` был POC и больше не является актуальным слоем.
Текущая реализация живет в `Loader.Script`.

Этот файл стоит переименовать в `script.md` или удалить после переноса нужной информации.

## Текущий pipeline

```text
Script
-> ScriptExecutor
-> LoadStatementExecutor per LOAD
-> provider reader
-> temp ClickHouse table
-> Query transformations
-> final ClickHouse table
-> LoadedTable metadata
```

## LOAD source

Файлы:

```text
orders:
LOAD
    id,
    Upper(name) AS name
FROM [orders.csv] (csv, delimiter=',', header=true)
WHERE id != '0'
ORDER BY id;
```

Базы данных:

```text
users:
LOAD
    id,
    name
FROM [Host=localhost;Database=app;Username=postgres;Password=postgres]
(postgres, table='public.users');
```

Поддерживаемые markers в `LoadProviderResolver`:

- `csv`
- `excel`, `xlsx`, `xls`, `xlsb`
- `json`
- `xml`
- `qvd`
- `postgres`, `postgresql`, `postgre`
- `sqlserver`, `mssql`
- `oracle`
- `hive`, `apachehive`, `apache-hive`
- `odbc`
- `clickhouse`

ODBC source:

```text
users:
LOAD
    id,
    name
FROM [Driver={ODBC Driver 18 for SQL Server};Server=localhost;Database=app;Uid=sa;Pwd=secret]
(odbc, table='dbo.users');
```

ODBC also supports a raw SQL query instead of `table`:

```text
users:
LOAD
    id,
    name
FROM [Dsn=analytics]
(odbc, sql='select id, name from "My Schema"."Users"');
```

## Telemetry

`Loader.Script` создает `ActivitySource` с именем `LoadScript`.

Основные activity:

- `Script.Statement`
- `LoadStatement.Prepare`
- `LoadStatement.TempTableWrite`
- `LoadStatement.QueryBuild`
- `LoadStatement.FinalTableWrite`

Для тегов, где может быть connection string, используется `activity?.SetTag(...).SetSanitizedTag(...)`.
Сейчас sanitizing применяется к `load.source` и скрывает `password`/`pwd`.

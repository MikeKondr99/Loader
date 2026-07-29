# Script

`Loader.Demo` был POC и больше не является актуальным слоем.
Текущая реализация живет в `Loader.Script`.

## LOAD pipeline

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

## CALENDAR pipeline

```text
CalendarStatement
-> CalendarStatementExecutor
-> literal range или MIN/MAX поля RESIDENT table
-> CREATE TABLE ... ENGINE = MergeTree ORDER BY column1 AS SELECT
-> LoadedTable metadata
```

`CALENDAR` генерируется непосредственно в целевом ClickHouse, без provider reader и staging table.
Даты строятся через `numbers(dateDiff(...) + 1)` и `addDays`, поэтому обе границы включены.

Для `RESIDENT` executor:

1. Находит единственную ранее загруженную таблицу по `LoadedTable.Alias`.
2. Разрешает логическое поле в `LoadedTable.Fields` и его физический ordinal `columnN`.
3. Разрешает только `Date`/`DateTime`.
4. Вычисляет количество non-null значений и `MIN/MAX(toDate(columnN))`.
5. Отклоняет пустой диапазон и материализует фиксированные 28 колонок.

Календарь регистрируется в `ScriptContext` как обычный `LoadedTable`, поэтому доступен последующим statement.
`RowCount` известен заранее, все поля имеют `CanBeNull = false`, а для логического поля `Date`
заполняются `Cardinality`, `Density`, `Min` и `Max`.

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
- `clickhouse`

## Telemetry

`Loader.Script` создает `ActivitySource` с именем `LoadScript`.

Основные activity:

- `Script.Statement`
- `LoadStatement.Prepare`
- `LoadStatement.TempTableWrite`
- `LoadStatement.QueryBuild`
- `LoadStatement.FinalTableWrite`
- `CalendarStatement.Execute`
- `CalendarStatement.FinalTableWrite`

Для тегов, где может быть connection string, используется `activity?.SetTag(...).SetSanitizedTag(...)`.
Сейчас sanitizing применяется к `load.source` и скрывает `password`/`pwd`.

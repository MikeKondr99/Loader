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
FROM Csv(path='orders.csv', delimiter=',', header=true)
WHERE id != '0'
ORDER BY id;
```

Базы данных:

```text
users:
LOAD
    id,
    name
FROM Connect(name='app_postgres')
SQL SELECT id, name FROM public.users;
```

Поддерживаемые provider calls в `LoadProviderResolver`:

- `Csv(path='...')`
- `Excel(path='...', sheet='...')`
- `Json(path='...', root='...')`
- `Xml(path='...', table='...')`
- `Qvd(path='...')`
- `Inline(...)`
- `Numbers(max=..., min=..., step=...)`
- `Calendar(min='...', max='...')`
- `Calendar(table='...', field='...')`
- `Connect(name='...')`

ODBC source:

```text
users:
LOAD
    id,
    name
FROM Connect(name='app_odbc_sqlserver')
SQL SELECT id, name FROM dbo.users;
```

ODBC uses the same SQL block style as other DB providers:

```text
users:
LOAD
    id,
    name
FROM Connect(name='analytics_odbc')
SQL select id, name from "My Schema"."Users";
```

DB connection strings не пишутся в script. Принимающая сторона передает `IConnectionRegistry`, где connection имеет
`name`, `ScriptConnectionType` и connection string. ODBC driver detection is best-effort and used for diagnostics.
A recognized driver kind does not mean the driver has full integration-test coverage; actual compatibility depends on
the installed ODBC driver and the SQL it accepts.

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

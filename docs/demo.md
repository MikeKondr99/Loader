# Loader.Demo

`Loader.Demo` выполняет один `LOAD` из script-файла:

```powershell
loader.exe script.txt
```

ClickHouse настраивается в `appsettings.json` рядом с executable или в текущей директории:

```json
{
  "ClickHouse": {
    "ConnectionString": "Host=localhost;Port=8123;Protocol=http;Database=default;Username=default;Password=",
    "Database": "default",
    "TablePrefix": "loader_demo_"
  }
}
```

## Pipeline

```text
FROM -> provider reader -> staging ClickHouse -> LOAD SELECT reader -> final ClickHouse -> CSV -> DROP staging
```

Физические колонки staging и final называются `column1`, `column2`, ... . Alias из LOAD
выводятся после выполнения как соответствие логическим именам вместе с фактическим доменным и
CLR-типом результата LOAD. Final-таблица получает имя
`loader_demo_result_<id>`, staging всегда удаляется через `DROP TABLE IF EXISTS`.

После создания final-таблицы Demo повторно читает ее из ClickHouse и потоково экспортирует в
CSV рядом со script-файлом. Например, для `orders.txt` результатом будет `orders.result.csv`.
Sylvan CsvDataWriter получает DbDataReader напрямую, поэтому exporter не хранит всю таблицу в памяти.

## Логи

Каждая строка содержит время от запуска процесса. Завершение этапа дополнительно показывает его
собственную длительность:

```text
[0.066 sec] Читаю скрипт
[0.072 sec] Скрипт прочитан заняло [0.006 sec]
```

## Файлы

Путь считается относительно script-файла. Provider для CSV, Excel, JSON и QVD определяется
по расширению; XML дополнительно требует имя элемента строки.

```text
LOAD id, city.Lower() AS city_lower
FROM [data/orders.csv] (delimiter=',', header=true);
```

```text
LOAD *
FROM [data/orders.xlsx] (sheet='Orders', header=true);
```

```text
LOAD *
FROM [data/orders.xml] (table='Order');
```

JSON в POC читается только как root array.

## Базы данных

Для БД source содержит connection string, marker option выбирает provider, а `table` задает
таблицу для простого `SELECT * FROM table`.

```text
LOAD id, name.Upper() AS upper_name
FROM [Host=localhost;Database=app;Username=postgres;Password=postgres]
(postgres, table='public.users');
```

Поддерживаемые marker-ы: `postgres`, `sqlserver`, `oracle`, `clickhouse`.

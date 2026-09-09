# Connect: Файл

Подключение `Файл` читает готовую таблицу или SQL-фрагмент в целевом DWH.

Это не файл на диске. Название означает файловую семантику подключения: данные уже загружены в DWH как результат внешнего файлового подключения.

## Пример

```ts
orders:
LOAD
  order_id,
  customer,
  amount
FROM Connect('dwh_orders_demo');
```

В скрипте не пишется `SQL` после `FROM Connect(...)`: SQL или имя таблицы уже заданы в настройке подключения.

## Настройка

Пример для Playground:

```json
{
  "Name": "dwh_orders_demo",
  "Type": "DwhFileTable",
  "Sql": "dwh_orders_demo"
}
```

`Sql` может быть именем таблицы:

```json
{
  "Name": "dwh_orders_demo",
  "Type": "DwhFileTable",
  "Sql": "dwh_orders_demo"
}
```

или SQL-запросом:

```json
{
  "Name": "dwh_orders_demo",
  "Type": "DwhFileTable",
  "Sql": "SELECT id, customer, amount FROM dwh_orders_demo"
}
```

Если `Sql` начинается с `SELECT` или `WITH`, Loader оборачивает его как подзапрос.

## CSV dump как текстовые колонки

Этот вид подключения полезен, когда внешний слой уже переложил файл в DWH, но оставил строки как текстовые колонки.

```ts
orders:
LOAD
  column1.Int() AS order_id,
  column2 AS customer,
  column3.Num() AS amount
FROM Connect('dwh_csv_dump_text')
ORDER BY order_id;
```

Если первая строка содержит header, ее нужно отфильтровать или пропустить на уровне запроса/источника данных. Для стабильного результата источник должен сам гарантировать порядок строк, например через `ORDER BY RowIndex` в своем SQL.

## Ошибки

`SQL` после `FROM Connect(...)` запрещен:

```ts
orders:
LOAD *
FROM Connect('dwh_orders_demo')
SQL SELECT * FROM orders;
```

Ошибка:

```text
Подключение Файл 'dwh_orders_demo' не поддерживает SQL после FROM.
```

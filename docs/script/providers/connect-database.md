# Connect: База данных

Подключение к БД выполняет SQL-запрос на стороне внешней базы данных.

## Пример

```ts
users:
LOAD
  id,
  name
FROM Connect('dev_postgres')
SQL
  SELECT id, name
  FROM public.users;
```

`SQL` после `FROM Connect(...)` обязателен для подключения к БД.

## Поддерживаемые типы

Поддерживаемые типы подключения:

| Type | Назначение |
| --- | --- |
| `Postgres` | PostgreSQL |
| `ClickHouse` | ClickHouse |
| `SqlServer` | Microsoft SQL Server |
| `Oracle` | Oracle |
| `Hive` | Apache Hive через ODBC |
| `Odbc` | ODBC-подключение |

## Правила

`Connect` для БД принимает только параметр `name`.

Запрос пишется в блоке `SQL` после `FROM Connect(...)`.

```ts
orders:
LOAD *
FROM Connect(name='dev_clickhouse')
SQL
  SELECT *
  FROM orders
  WHERE created_at >= toDate('2024-01-01');
```

`SQL`-блок является запросом к источнику. Преобразования `LOAD` выполняются уже после чтения результата этого запроса.

Если нужны сложные преобразования Loader, удобнее сделать два шага:

```ts
raw_orders:
TEMP LOAD *
FROM Connect('dev_postgres')
SQL
  SELECT id, amount, created_at
  FROM public.orders;

orders:
LOAD
  id,
  amount.Num() AS amount,
  created_at.Date() AS created_at
FROM raw_orders
WHERE amount > 0;
```

## Ошибки

Если `SQL` не указан:

```text
Для провайдера БД 'connect' требуется SQL после FROM.
```

Если тип подключения не поддерживается:

```text
Connection '...' использует неподдерживаемый провайдер '...'.
```

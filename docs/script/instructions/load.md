# LOAD

`LOAD` читает источник, вычисляет поля и создает script-таблицу.

## Синтаксис

```text
table_name:
FIRST N
TEMP | MAPPED
LOAD
  * | expression AS field_name, field_name
FROM source
SQL source_sql
;
```

или с LOAD-level преобразованиями:

```text
table_name:
FIRST N
TEMP | MAPPED
LOAD
  * | expression AS field_name, field_name
FROM source
WHERE expression
GROUP BY expression, ...
ORDER BY expression ASC|DESC, ...
LIMIT N OFFSET M
;
```

`table_name:` обязателен.

`FIRST`, `TEMP` и `MAPPED` необязательны. Если они есть, порядок такой: `FIRST`, затем `TEMP` или `MAPPED`, затем `LOAD`.

## Минимальный Пример

```ts
orders:
LOAD *
FROM Csv('orders.csv');
```

## Поля

`LOAD *` берет все поля source.

Явный список полей может содержать:

- короткую форму `field_name`;
- выражение с обязательным alias: `expression AS field_name`.

```ts
orders:
LOAD
  id,
  amount.Num() AS amount,
  amount.Num() * quantity.Int() AS total
FROM Csv('orders.csv');
```

Короткая форма `id` эквивалентна `id AS id`.

Alias-ы select-полей не должны повторяться.

## Видимость Полей

В начале `LOAD` доступны поля source: поля провайдера или поля предыдущей script-таблицы.

Поле, созданное через `expression AS alias`, добавляется в контекст текущего `LOAD`. Поэтому на него можно ссылаться дальше в этой же инструкции: в следующих полях, `WHERE`, `GROUP BY` и `ORDER BY`.

```ts
orders:
LOAD
  amount.Num() AS amount,
  quantity.Int() AS quantity,
  amount * quantity AS total
FROM Csv('orders.csv')
WHERE amount > 0
ORDER BY total DESC;
```

Если alias совпадает с именем поля source, новый alias затеняет исходное имя для последующих обращений в текущем `LOAD`. Чтобы не получать неочевидное поведение, лучше давать преобразованным полям отдельные имена, когда нужно обращаться и к исходному, и к вычисленному значению.

## Source

`FROM` принимает вызов провайдера:

```ts
FROM Csv('orders.csv')
FROM Connect('dev_postgres') SQL SELECT * FROM orders
```

или имя уже загруженной script-таблицы:

```ts
FROM raw_orders
```

`FROM raw_orders` эквивалентен `FROM Table(name='raw_orders')`.

## Когда Выполняется

`LOAD` выполняется во время исполнения сценария, когда executor доходит до этой инструкции.

Подробный pipeline описан в [Модели выполнения](../execution-model.md). В этом разделе зафиксированы правила самой инструкции: синтаксис, допустимые clauses и побочные эффекты.

## FIRST

`FIRST N` ограничивает исходные строки source до вычислений `LOAD`.

```ts
sample:
FIRST 100
LOAD *
FROM Csv('orders.csv');
```

Для reader-source это ограничивает строки до записи во временную таблицу. Для DWH-source применяется внешняя SQL-обертка с `LIMIT`.

## TEMP LOAD

`TEMP LOAD` создает таблицу, доступную следующим инструкциям, но удаляемую после выполнения сценария.

```ts
raw:
TEMP LOAD *
FROM Csv('orders.csv');

result:
LOAD
  id.Int() AS id
FROM raw;
```

`TEMP`-таблицы не возвращаются как итоговые таблицы сценария.

## MAPPED LOAD

`MAPPED LOAD` создает mapping table для lookup-функций.

```ts
map_status:
MAPPED LOAD
  code AS key,
  name AS value
FROM Inline(code, name; 'A', 'Active'; 'B', 'Blocked');
```

`MAPPED LOAD` должен возвращать ровно два поля. Для `LOAD *` source тоже должен иметь ровно два поля.

`MAPPED`-таблицы очищаются как временные.

## SQL После FROM

`SQL` после `FROM` используется для подключений к БД:

```ts
users:
LOAD *
FROM Connect('dev_postgres')
SQL
  SELECT id, name
  FROM public.users;
```

Если указан `SQL`, то `WHERE`, `GROUP BY`, `ORDER BY`, `LIMIT`, `OFFSET` после него являются частью SQL источника и не парсятся как clauses уровня `LOAD`.

Для большинства файловых и встроенных провайдеров `SQL` после `FROM` запрещен.

## WHERE

`WHERE` фильтрует строки LOAD query.

```ts
orders:
LOAD *
FROM raw_orders
WHERE amount > 0;
```

`WHERE` работает после подготовки источника. Если источник был внешним reader-ом, данные уже записаны во временную таблицу.

## GROUP BY

`GROUP BY` группирует строки LOAD query и используется вместе с агрегатными функциями.

```ts
sales:
LOAD
  city,
  Sum(amount) AS amount
FROM orders
GROUP BY city;
```

Выражения в `LOAD`, `WHERE`, `GROUP BY`, `ORDER BY` проверяются resolver-ом на совместимость с группировкой и агрегатами.

## ORDER BY

`ORDER BY` задает порядок результата LOAD query.

```ts
orders:
LOAD *
FROM raw_orders
ORDER BY order_date DESC, order_id ASC;
```

Без `ORDER BY` порядок строк не считается частью контракта.

## LIMIT И OFFSET

`LIMIT` ограничивает результат LOAD query.

`OFFSET` допускается только вместе с `LIMIT`.

```ts
sample:
LOAD *
FROM raw_orders
ORDER BY order_id
LIMIT 100 OFFSET 200;
```

В отличие от `FIRST`, `LIMIT` применяется к результату query после `WHERE`, `GROUP BY` и `ORDER BY`.

## Побочные Эффекты

`LOAD` создает физическую таблицу в ClickHouse и добавляет `LoadedTable` в контекст сценария.

Если `LOAD` завершается ошибкой после создания финальной таблицы, executor пытается удалить ее best-effort.

Нельзя создать новую активную таблицу с уже занятым alias.

## Ошибки

Типовые ошибки:

- таблица с таким alias уже есть;
- провайдер источника не найден или неверно настроен;
- source вернул дублирующиеся имена полей;
- select alias повторяется;
- поле или функция в выражении не найдены;
- `MAPPED LOAD` вернул не два поля;
- ClickHouse не смог материализовать финальную таблицу.

## Производительность

`FIRST` полезен для sample-загрузки, потому ограничивает source до основных преобразований.

Для внешних reader-source все равно создается временная таблица. Для DWH-source можно избежать отдельной временной таблицы перед LOAD query.

`WHERE` внутри `LOAD` не уменьшает объем чтения внешнего файла, если источник уже был загружен во временную таблицу. Для БД лучше фильтровать в SQL источника, если нужно сократить чтение из внешней БД.

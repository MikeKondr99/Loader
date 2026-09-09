# Join

`Join`, `LeftJoin`, `RightJoin` и `FullJoin` соединяют две уже загруженные script-таблицы.

## Минимальный пример

```ts
orders:
LOAD *
FROM Inline(id, customer_id, amount; 1, 10, 100; 2, 20, 200);

customers:
LOAD *
FROM Inline(id, name; 10, 'Alice'; 30, 'Charlie');

result:
LOAD *
FROM LeftJoin(orders, customer_id, customers, id);
```

## Виды join

| Provider | ClickHouse join |
| --- | --- |
| `Join` | `INNER JOIN` |
| `LeftJoin` | `LEFT JOIN` |
| `RightJoin` | `RIGHT JOIN` |
| `FullJoin` | `FULL OUTER JOIN` |

## Синтаксис

```ts
Join(leftTable, leftField, rightTable, rightField)
LeftJoin(leftTable, leftField, rightTable, rightField)
RightJoin(leftTable, leftField, rightTable, rightField)
FullJoin(leftTable, leftField, rightTable, rightField)
```

Provider принимает ровно четыре позиционных имени без кавычек.

Для имен с пробелами или спецсимволами используйте bracket-name:

```ts
result:
LOAD *
FROM Join([orders table], [customer id], [customer table], [customer id]);
```

## Требования

Обе таблицы должны быть уже загружены предыдущими `LOAD`.

Ключевые поля должны существовать и иметь одинаковый доменный тип.

Self-join одной и той же script-таблицы не поддерживается напрямую. Если нужно соединить таблицу саму с собой, загрузите ее вторым `LOAD` под другим alias.

## Имена полей

Если имена полей не конфликтуют, они остаются без изменений.

Если одно имя есть в обеих таблицах, результат получает префикс alias таблицы:

```text
orders.id
customers.id
```

Полный пример:

```ts
orders:
LOAD *
FROM Inline(id, customer_id; 1, 10);

customers:
LOAD *
FROM Inline(id, name; 10, 'Alice');

result:
LOAD
  [orders.id] AS order_id,
  customer_id,
  [customers.id] AS customer_id_from_dict,
  name
FROM Join(orders, customer_id, customers, id);
```

В результате `id` из `orders` доступен как `[orders.id]`, а `id` из `customers` как `[customers.id]`.

Если после такого переименования конфликт все равно остается, provider вернет ошибку.

Для outer join поля стороны, где может отсутствовать строка, становятся nullable. `FullJoin` делает nullable поля обеих сторон.

SQL после `FROM Join(...)` и других join-provider-ов не поддерживается.

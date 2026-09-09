# Table

`Table` читает результат уже выполненного `LOAD`.

Обычно используется короткий синтаксис `FROM table_alias`.

## Минимальный пример

```ts
raw:
LOAD *
FROM Csv('orders.csv');

orders:
LOAD
  id.Int() AS order_id,
  amount.Num() AS amount
FROM raw;
```

`FROM raw` парсится как `FROM Table(name='raw')`.

## Явный вызов

```ts
orders:
LOAD *
FROM Table(name='raw');
```

## Параметры

| Параметр | Тип | По умолчанию | Описание |
| --- | --- | --- | --- |
| `name` | `text` | обязателен | Alias таблицы, созданной предыдущим `LOAD`. |

## Особенности

`Table` работает только с таблицами, которые уже есть в текущем `ScriptContext`.

Если исходная таблица была создана как `TEMP LOAD`, она будет доступна следующим LOAD-ам, но будет очищена в конце выполнения скрипта.

SQL после `FROM Table(...)` не поддерживается.

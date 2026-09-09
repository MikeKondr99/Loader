# Inline

`Inline` задает небольшую таблицу прямо в скрипте.

## Минимальный пример

```ts
users:
LOAD *
FROM Inline(
  id, name, active;
  1, 'Alice', true;
  2, 'Bob', false
);
```

## Синтаксис

```ts
Inline(column1, column2; value1, value2; value3, value4)
```

До первой `;` идет header. После него идут строки данных.

Строковые значения нужно писать в кавычках:

```ts
FROM Inline(city, amount; 'Berlin', 10; 'Paris', 20);
```

Числа можно писать положительными и отрицательными:

```ts
FROM Inline(id, amount; 1, 10.5; -2, -20);
```

## Типы

Тип колонки выводится по значениям:

| Значения | Тип |
| --- | --- |
| `1`, `2`, `null` | `Integer` |
| `1`, `2.5` | `Number` |
| `true`, `false` | `Boolean` |
| `'text'`, `null` | `Text` |
| несовместимая смесь, например `1` и `'x'` | `Text` |

`null` делает колонку nullable.

`Inline` не умеет задавать date/time literals напрямую. Даты и время нужно передавать как строки и преобразовывать в `LOAD`:

```ts
events:
LOAD
  Date(date) AS date,
  Time(time) AS time
FROM Inline(
  date,         time;
  '2010-01-01', '10:40'
);
```

## Ограничения

`Inline` не поддерживает `WHERE`, `GROUP BY`, `ORDER BY`, `LIMIT`, `OFFSET` и SQL сразу после `FROM`.

Если нужны преобразования, сначала загрузите `Inline` в отдельную таблицу:

```ts
raw:
TEMP LOAD *
FROM Inline(
  city, amount;
  'Berlin', 10;
  'Paris', 20;
  'Rome', 15
);

result:
LOAD
  city,
  amount
FROM raw
WHERE amount > 10
ORDER BY amount DESC;
```

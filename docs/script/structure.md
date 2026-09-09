# Структура Сценария

Сценарий состоит из последовательности инструкций. Инструкции выполняются сверху вниз, в том порядке, в котором написаны.

Поддерживаемые инструкции:

- `LOAD` - читает источник, вычисляет поля и создает script-таблицу.
- `DROP` - удаляет ранее созданную script-таблицу.

Каждая инструкция заканчивается `;`.

## Общая форма

```text
инструкция;
инструкция;
инструкция;
```

Пример:

```ts
raw_orders:
TEMP LOAD *
FROM Csv('orders.csv');

orders:
LOAD
  id.Int() AS order_id,
  amount.Num() AS amount
FROM raw_orders
WHERE amount > 0;

DROP raw_orders;
```

## Имена

Обычное имя начинается с буквы или `_` и может содержать буквы, цифры и `_`:

```ts
orders_2026:
LOAD *
FROM Csv('orders.csv');
```

Если в имени есть пробелы, точки, дефисы или другие специальные символы, используйте bracket-name:

```ts
[orders 2026]:
LOAD *
FROM Csv('orders.csv');

result:
LOAD
  [order-id] AS order_id
FROM [orders 2026];
```

## Комментарии

Поддерживаются line comments и block comments:

```ts
// строковый комментарий

/*
  блочный комментарий
*/
```

## Выражения

Выражения используются в полях `LOAD`, `WHERE`, `GROUP BY`, `ORDER BY` и аргументах функций.

Примеры:

```ts
LOAD
  amount.Num() AS amount,
  amount.Num() * quantity.Int() AS total,
  If(active, 'yes', 'no') AS status
FROM source
WHERE amount.Num() > 0
ORDER BY total DESC;
```

## Функции

Имена функций чувствительны к регистру. Нужно писать имя в той форме, в которой функция определена в Loader:

```ts
LOAD
  If(active, 'yes', 'no') AS status,
  Upper(name) AS name
FROM source;
```

Функции могут поддерживать обычную форму, методную форму или обе формы.

Обычная форма:

```ts
Int(id)
If(active, 'yes', 'no')
SUM(amount)
```

Методная форма:

```ts
id.Int()
name.Upper()
amount.Alt(0)
```

Многие методные функции также можно вызвать в обычной форме, например `Int(id)` и `id.Int()`. Но это не общее правило для всех функций: `If` вызывается как `If(condition, then, else)`, а агрегаты вроде `SUM`, `AVG`, `COUNT` вызываются как обычные функции.

## Строки

Строки пишутся в одинарных кавычках:

```ts
'text'
'orders.csv'
```

Escape-последовательности пишутся через `\`.

Внутри строки можно использовать интерполяцию `${expr}`:

```ts
'report_${Today().Text('yyyy-MM-dd')}.csv'
```

Qlik-style переменные вида `$()` сейчас не описаны как часть языка сценариев.

## Таблицы Сценария

`LOAD` создает script-таблицу с alias из префикса перед `LOAD`:

```ts
orders:
LOAD *
FROM Csv('orders.csv');
```

Следующие инструкции могут обращаться к ней по имени:

```ts
result:
LOAD *
FROM orders;
```

Нельзя создать две активные таблицы с одинаковым alias. Если таблицу удалить через `DROP`, имя можно использовать снова.

# Lang

## Script

`Script.Parse(text)` парсит несколько statement.
Пока поддерживается только `LOAD`, но модель рассчитана на расширение через наследников `Statement`.

## LOAD statement

`LOAD` сейчас парсится в `LoadStatement`.

```text
LOAD * FROM [orders.csv];
```

Для формы `LOAD *` поле `LoadStatement.Fields` равно `null`. Это означает “взять все поля из source”.

Имя результирующей таблицы задается префиксом перед `LOAD`:

```text
orders:
LOAD * FROM [orders.csv];
```

В AST это `LoadStatement.TableName = "orders"`.

```text
LOAD
    amount * 1.2 AS gross_amount,
    city.Lower() AS city,
FROM [orders.csv] (csv, delimiter=',', header=true);
```

Для явного списка `LoadStatement.Fields` содержит поля в порядке из скрипта. Trailing comma разрешена.

Короткая форма поля разворачивается на уровне парсинга:

```text
LOAD id FROM [orders.csv];
```

В AST это становится полем `id AS id`: `Name = "id"`, `Expression = NameExpr("id")`.

`LoadStatement.Options` содержит provider/source options. Как в Qlik, options внутри скобок разделяются запятыми:

```text
(csv, delimiter=',', header=true)
```

- marker option: `csv` -> `Value = null`
- value option: `delimiter=','` -> `Value = StringLiteral(",")`
- option value может быть только `string`, `integer`, `number`, `boolean`
- `name` и `null` как option value запрещены
- пропущенная запятая между options запрещена

## Clauses

Поддерживаемые части `LOAD`:

```text
orders:
LOAD
    id,
    Num(amount) AS amount
FROM [orders.csv] (csv)
WHERE amount > 0
GROUP BY id
ORDER BY id DESC
LIMIT 100
OFFSET 10;
```

- `Where` хранится как `Expr?`.
- `GroupBy` хранится как `List<Expr>?`; `null` означает отсутствие `GROUP BY`.
- `OrderBy` хранится как `List<LoadOrderField>?`; `null` означает отсутствие `ORDER BY`.
- `Limit` и `Offset` хранятся как `long?`.
- `OFFSET` допускается только после `LIMIT`.

## Имена и keywords

Обычные keywords не парсятся как bare names.
Если нужно имя, совпадающее с keyword, используется blocked name:

```text
LOAD [where] FROM [orders.csv];
```

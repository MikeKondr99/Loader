# Lang

## LOAD statement

`LOAD` сейчас парсится в `LoadStatement`.

```text
LOAD * FROM [orders.csv];
```

Для формы `LOAD *` поле `LoadStatement.Fields` равно `null`. Это означает “взять все поля из source”.

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

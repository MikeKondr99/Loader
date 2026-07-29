# Lang

## Script

`Script.Parse(text)` парсит несколько statement.
Поддерживаются `LOAD` и `CALENDAR`; оба представлены наследниками `Statement`.

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

Исключение — префикс `Calendar:`: он разрешен специально, чтобы имя календаря не требовало escaping.
Новые слова `CALENDAR`, `TO`, `FIELD`, `RESIDENT` также остаются допустимыми в однозначных позициях имен,
чтобы добавление statement не ломало существующие поля с такими именами.

## CALENDAR statement

`CALENDAR` создает материализованную таблицу с одной строкой на каждый день включительного диапазона.
Имя результирующей таблицы обязательно.

Явный диапазон задается ISO-датами:

```text
Calendar:
CALENDAR
FROM '2024-01-01'
TO '2024-12-31';
```

Дата должна быть обычным строковым литералом строго в формате `yyyy-MM-dd`.
Интерполяция и другие форматы не допускаются.

Диапазон можно вычислить по полю ранее загруженной таблицы:

```text
Orders:
LOAD
    Date(created_at, 'yyyy-MM-dd') AS CreatedAt
FROM [orders.csv] (csv);

Calendar:
CALENDAR
FROM FIELD CreatedAt
RESIDENT Orders;
```

- `RESIDENT` ссылается только на таблицу, созданную предыдущим statement того же script.
- Alias таблицы и имя поля сравниваются с учетом регистра.
- Alias таблицы должен разрешаться однозначно.
- Поле должно иметь тип `Date` или `DateTime`; для `DateTime` используется календарная дата.
- `NULL` не участвуют в `MIN/MAX`. Пустая таблица или поле без non-null дат считается ошибкой.
- Диапазон включителен и должен помещаться в ClickHouse `Date`: `1970-01-01`–`2149-06-06`.

Публичная схема календаря фиксирована:

```text
Date, Year, QuarterNumber, Quarter, YearQuarterNumber, YearQuarter,
MonthNumber, MonthName, MonthShortName, YearMonthNumber, YearMonth,
MonthYear, WeekNumber, YearWeek, StartOfWeek, LastDayOfWeek,
DayOfWeek, DayOfWeekName, DayOfMonth, DayOfYear, StartOfYear,
EndOfYear, StartOfQuarter, EndOfQuarter, StartOfMonth, EndOfMonth,
DayMonth, WeekPeriod
```

Физические колонки следуют общей стратегии materialization и называются
`column1`–`column28`; логические имена и типы хранятся в `LoadedTable.Fields`.

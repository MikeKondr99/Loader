# Calendar

`Calendar` генерирует календарную таблицу в DWH SQL.

Есть два режима:

- явный диапазон дат: `Calendar(min='2024-01-01', max='2024-01-31')`;
- диапазон по min/max значениям поля уже загруженной таблицы: `Calendar(table=orders, field=CreatedAt)`.

## Явный диапазон

```ts
calendar:
LOAD *
FROM Calendar(min='2024-01-01', max='2024-01-03');
```

Позиционный вариант:

```ts
calendar:
LOAD *
FROM Calendar('2024-01-01', '2024-01-03');
```

## Диапазон из таблицы

```ts
orders:
LOAD
  Date(DateText, 'yyyy-MM-dd') AS CreatedAt
FROM Csv('orders.csv');

calendar:
LOAD *
FROM Calendar(table=orders, field=CreatedAt);
```

Позиционный вариант:

```ts
calendar:
LOAD *
FROM Calendar(orders, CreatedAt);
```

Так можно вычислять диапазон по любой формуле: сначала сделать маленькую таблицу с датами, потом построить календарь по `min/max` этого поля.

```ts
range:
TEMP LOAD
  If(number = 0, Date('2023-01-01'), Today()) AS date
FROM Numbers(1);

calendar:
LOAD *
FROM Calendar(range, date);
```

## Параметры

| Параметр | Тип | По умолчанию | Описание |
| --- | --- | --- | --- |
| `min` | `text` | обязателен для явного диапазона | Начальная дата в формате `yyyy-MM-dd`. |
| `max` | `text` | обязателен для явного диапазона | Конечная дата в формате `yyyy-MM-dd`. |
| `table` | `name` | обязателен для режима table/field | Alias уже загруженной LOAD-таблицы. |
| `field` | `name` | обязателен для режима table/field | Поле типа `Date` или `DateTime` в таблице `table`. |

Нельзя смешивать `min/max` и `table/field` в одном вызове.

## Поля результата

`Calendar` возвращает календарные поля:

| Поле | Тип |
| --- | --- |
| `Date` | `DateTime` |
| `Year` | `Integer` |
| `QuarterNumber` | `Integer` |
| `Quarter` | `Text` |
| `YearQuarterNumber` | `Integer` |
| `YearQuarter` | `Text` |
| `MonthNumber` | `Integer` |
| `MonthName` | `Text` |
| `MonthShortName` | `Text` |
| `YearMonthNumber` | `Integer` |
| `YearMonth` | `Text` |
| `MonthYear` | `Text` |
| `WeekNumber` | `Integer` |
| `YearWeek` | `Text` |
| `StartOfWeek` | `DateTime` |
| `LastDayOfWeek` | `DateTime` |
| `DayOfWeek` | `Integer` |
| `DayOfWeekName` | `Text` |
| `DayOfMonth` | `Integer` |
| `DayOfYear` | `Integer` |
| `StartOfYear` | `DateTime` |
| `EndOfYear` | `DateTime` |
| `StartOfQuarter` | `DateTime` |
| `EndOfQuarter` | `DateTime` |
| `StartOfMonth` | `DateTime` |
| `EndOfMonth` | `DateTime` |
| `DayMonth` | `Text` |
| `WeekPeriod` | `Text` |

## Ограничения

Явный диапазон должен попадать в безопасный диапазон `1970-01-05..2148-12-31`.

SQL после `FROM Calendar(...)` не поддерживается.

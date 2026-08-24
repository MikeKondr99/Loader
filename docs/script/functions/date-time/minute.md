# Minute

`Minute` возвращает минуту из `date` или `time`.

## Minute(date)

`Minute(date)` возвращает минуты из date-time значения.

Если входная дата равна `null`, результат тоже `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `Date('2023-05-15 14:30:22').Minute()` | `30` |
| `Date('2023-05-15 14:00:22').Minute()` | `0` |
| `Date('2023-05-15 14:59:22').Minute()` | `59` |
| `Date(null).Minute()` | `null` |

## Minute(time)

`Minute(time)` возвращает минуты из времени суток.

Если входное время равно `null`, результат тоже `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `Time('03:04:05').Minute()` | `4` |
| `Time('03:00:05').Minute()` | `0` |
| `Time('03:59:05').Minute()` | `59` |
| `Time(null).Minute()` | `null` |

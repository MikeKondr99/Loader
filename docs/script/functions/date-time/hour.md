# Hour

`Hour` возвращает час из `date` или `time`.

## Hour(date)

`Hour(date)` возвращает час из date-time значения.

Если входная дата равна `null`, результат тоже `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `Date('2023-05-15 14:30:22').Hour()` | `14` |
| `Date('2023-05-15 00:30:22').Hour()` | `0` |
| `Date('2023-05-15 23:30:22').Hour()` | `23` |
| `Date(null).Hour()` | `null` |

## Hour(time)

`Hour(time)` возвращает час из времени суток.

Если входное время равно `null`, результат тоже `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `Time('03:04:05').Hour()` | `3` |
| `Time('00:04:05').Hour()` | `0` |
| `Time('23:04:05').Hour()` | `23` |
| `Time(null).Hour()` | `null` |

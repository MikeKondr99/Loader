# Second

`Second` возвращает секунду из `date` или `time`.

## Second(date)

`Second(date)` возвращает секунды из date-time значения.

Если входная дата равна `null`, результат тоже `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `Date('2023-05-15 14:30:22').Second()` | `22` |
| `Date('2023-05-15 14:30:00').Second()` | `0` |
| `Date('2023-05-15 14:30:59').Second()` | `59` |
| `Date(null).Second()` | `null` |

## Second(time)

`Second(time)` возвращает секунды из времени суток.

Если входное время равно `null`, результат тоже `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `Time('03:04:05').Second()` | `5` |
| `Time('03:04:00').Second()` | `0` |
| `Time('03:04:59').Second()` | `59` |
| `Time(null).Second()` | `null` |

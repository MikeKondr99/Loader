# Month

`Month` возвращает месяц из `date`.

## Month(date)

`Month(date)` возвращает номер месяца `1..12`.

Если входная дата равна `null`, результат тоже `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `Date('2023-05-15').Month()` | `5` |
| `Date('2023-12-31').Month()` | `12` |
| `Date(null).Month()` | `null` |

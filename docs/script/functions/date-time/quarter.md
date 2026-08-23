# Quarter

`Quarter` возвращает квартал года из `date`.

## Quarter(date)

`Quarter(date)` возвращает номер квартала `1..4`.

Если входная дата равна `null`, результат тоже `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `Date('2023-01-15').Quarter()` | `1` |
| `Date('2023-04-01').Quarter()` | `2` |
| `Date('2023-07-15').Quarter()` | `3` |
| `Date('2023-10-31').Quarter()` | `4` |
| `Date(null).Quarter()` | `null` |

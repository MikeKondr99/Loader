# DayOfYear

`DayOfYear` возвращает номер дня в году из `date`.

## DayOfYear(date)

`DayOfYear(date)` возвращает номер дня в году, начиная с `1`.

Если входная дата равна `null`, результат тоже `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `Date('2023-01-01').DayOfYear()` | `1` |
| `Date('2023-12-31').DayOfYear()` | `365` |
| `Date('2024-12-31').DayOfYear()` | `366` |
| `Date(null).DayOfYear()` | `null` |

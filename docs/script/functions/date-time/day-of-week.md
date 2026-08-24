# DayOfWeek

`DayOfWeek` возвращает день недели из `date`.

## DayOfWeek(date)

`DayOfWeek(date)` возвращает день недели в ISO-порядке: понедельник `1`, воскресенье `7`.

Если входная дата равна `null`, результат тоже `null`.

Если нужна неделя с воскресенья, можно преобразовать результат:

```text
Rem(date.DayOfWeek(), 7)      // воскресенье = 0, понедельник = 1, ..., суббота = 6
Rem(date.DayOfWeek(), 7) + 1  // воскресенье = 1, понедельник = 2, ..., суббота = 7
```

Примеры:

| Expression | Result |
| --- | --- |
| `Date('2023-05-15').DayOfWeek()` | `1` |
| `Date('2023-05-20').DayOfWeek()` | `6` |
| `Date('2023-05-21').DayOfWeek()` | `7` |
| `Rem(Date('2023-05-21').DayOfWeek(), 7)` | `0` |
| `Rem(Date('2023-05-21').DayOfWeek(), 7) + 1` | `1` |
| `Date(null).DayOfWeek()` | `null` |

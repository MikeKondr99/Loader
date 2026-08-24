# DateOnly

`DateOnly` возвращает дату без времени.

## DateOnly(date)

`DateOnly(date)` обнуляет время внутри `date`.

Результат остается доменным типом `date`, технически представленным как date-time со временем `00:00:00`.

Примеры:

| Expression | Result |
| --- | --- |
| `Date('2026-01-02 15:04:05').DateOnly().Text()` | `'2026-01-02 00:00:00'` |
| `Date('2026-01-02', 'yyyy-MM-dd').DateOnly().Text('yyyy-MM-dd')` | `'2026-01-02'` |
| `Date(null, 'yyyy-MM-dd').DateOnly().Text('yyyy-MM-dd')` | `null` |

# YearMonth

`YearMonth` возвращает год и месяц из `date`.

## YearMonth(date)

`YearMonth(date)` возвращает текст в формате `YYYY-MM`.

По смыслу это готовый shortcut для `date.Text('yyyy-MM')`.

Если входная дата равна `null`, результат тоже `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `Date('2023-05-15').YearMonth()` | `'2023-05'` |
| `Date('2023-01-01 14:30:22').YearMonth()` | `'2023-01'` |
| `Date('2023-05-15').YearMonth() = Date('2023-05-15').Text('yyyy-MM')` | `true` |
| `Date(null).YearMonth()` | `null` |

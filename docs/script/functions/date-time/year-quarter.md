# YearQuarter

`YearQuarter` возвращает год и квартал из `date`.

## YearQuarter(date)

`YearQuarter(date)` возвращает текст в формате `YYYY-QN`.

По смыслу это готовый shortcut для сборки `date.Text('yyyy') + '-Q' + Text(date.Quarter())`.

Если входная дата равна `null`, результат тоже `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `Date('2023-01-15').YearQuarter()` | `'2023-Q1'` |
| `Date('2023-04-01').YearQuarter()` | `'2023-Q2'` |
| `Date('2023-04-01').YearQuarter() = Date('2023-04-01').Text('yyyy') + '-Q' + Text(Date('2023-04-01').Quarter())` | `true` |
| `Date(null).YearQuarter()` | `null` |

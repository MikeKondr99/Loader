# FutureValue

`FutureValue` вычисляет будущую стоимость серии постоянных платежей при постоянной процентной ставке.

## FutureValue(rate, nper, pmt)

`FutureValue(rate, nper, pmt)` принимает:

- `rate` - процентная ставка за период как `num`.
- `nper` - количество периодов как `int`.
- `pmt` - платеж за период как `num`.

Результат имеет тип `num`.

Если любой аргумент равен `null`, результат тоже `null`.

TODO: определить отдельную семантику для `rate = 0`. Сейчас функция использует общую формулу с делением на `rate`.

Примеры:

| Expression | Result |
| --- | --- |
| `FutureValue(0.005, 36, -20.0).Text().Substring(1, 9)` | `'786.72209'` |
| `FutureValue(null, 36, -20.0)` | `null` |
| `FutureValue(0.005, null, -20.0)` | `null` |
| `FutureValue(0.005, 36, null)` | `null` |

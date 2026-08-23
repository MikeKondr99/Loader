# Power

`Pow` и оператор `^` возводят число в степень.

## Pow(value, power)

`Pow(value, power)` возвращает `value` в степени `power`.

Оба аргумента имеют тип `num`, результат тоже `num`.

Если один из аргументов равен `null`, результат тоже `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `Pow(2.0, 3.0)` | `8.0` |
| `Pow(4.0, 0.5)` | `2.0` |
| `Pow(2.0, -1.0)` | `0.5` |
| `Pow(null, 2.0)` | `null` |
| `Pow(2.0, null)` | `null` |

## value ^ power

`^` делает то же самое, что и `Pow(value, power)`.

Примеры:

| Expression | Result |
| --- | --- |
| `2.0 ^ 3.0` | `8.0` |

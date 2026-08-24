# Number Functions

Number functions работают с доменными типами `int` и `num`.

## Функции

- [Arithmetic](/docs/script/functions/numbers/arithmetic.md) - `+`, `-`, unary `-`, `*`, `/`.
- [Power](/docs/script/functions/numbers/power.md) - `Pow` и оператор `^`.
- [Constants](/docs/script/functions/numbers/constants.md) - `Pi`, `E`.
- [Round / Floor / Ceil](/docs/script/functions/numbers/round.md) - округление до целого, шага или шага со смещением.
- [Mod / Rem](/docs/script/functions/numbers/remainder.md) - остаток от деления.
- [Abs / Sign](/docs/script/functions/numbers/abs-sign.md) - модуль и знак числа.
- [Even / Odd](/docs/script/functions/numbers/even-odd.md) - проверка четности.
- [Frac](/docs/script/functions/numbers/frac.md) - дробная часть числа.

## Общая семантика

`int` операции возвращают `int`, если оба операнда остаются целыми.

`num` операции возвращают `num`. Если один из операндов приводится к `num`, результат обычно тоже `num`.

Если входной аргумент равен `null`, большинство number functions возвращает `null`. Исключения и особые случаи описываются на странице конкретной функции.

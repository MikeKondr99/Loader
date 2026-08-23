# Acosh

`Acosh` возвращает гиперболический арккосинус.

## Acosh(value)

`Acosh(value)` принимает `num` больше или равный `1` и возвращает `num`.

Если входной аргумент равен `null`, результат тоже `null`.

Если `value` меньше `1`, результат `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `Acosh(1)` | `0.0` |
| `Acosh(2)` | `1.3169578969248166` |
| `Acosh(0.5)` | `null` |
| `Acosh(-1)` | `null` |
| `Acosh(null)` | `null` |

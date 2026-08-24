# Logic

Логические операции работают с `bool`.

## true / false

`true` и `false` - булевы литералы.

Примеры:

| Expression | Result |
| --- | --- |
| `true` | `true` |
| `false` | `false` |

## Not(value)

`Not(value)` возвращает логическое отрицание.

Если вход равен `null`, результат тоже `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `Not(true)` | `false` |
| `Not(false)` | `true` |
| `Not(null)` | `null` |

## left and right

`and` возвращает `true`, только если оба аргумента `true`.

Если один из аргументов `false`, результат `false`. Если результат нельзя определить из-за `null`, результат `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `true and true` | `true` |
| `true and false` | `false` |
| `false and false` | `false` |
| `true and null` | `null` |
| `false and null` | `false` |

## left or right

`or` возвращает `true`, если хотя бы один аргумент `true`.

Если один из аргументов `true`, результат `true`. Если результат нельзя определить из-за `null`, результат `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `true or true` | `true` |
| `true or false` | `true` |
| `false or false` | `false` |
| `true or null` | `true` |
| `false or null` | `null` |

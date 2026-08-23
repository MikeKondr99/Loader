# PadLeft, PadRight

`PadLeft` и `PadRight` дополняют строку до заданной длины.

Если исходная строка длиннее или равна целевой длине, текущая реализация обрезает результат до `count`.

Если `count` отрицательный или `null`, текущий результат - пустая строка.

TODO: решить, должно ли значение обрезаться, если `count` меньше длины строки.

TODO: решить, должен ли `PadLeft(value, null)` / `PadRight(value, null)` возвращать `null`, а не пустую строку.

## PadLeft(value, count)

`PadLeft(value, count)` дополняет строку слева обычными пробелами.

Если `value` равен `null`, результат тоже `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `PadLeft('abc', 5)` | `'  abc'` |
| `PadLeft('abc', 3)` | `'abc'` |
| `PadLeft('abc', 2)` | `'ab'` |
| `PadLeft('abc', 0)` | `''` |
| `PadLeft('', 5)` | `'     '` |
| `PadLeft('привет', 8)` | `'  привет'` |
| `PadLeft('😀', 3)` | `'  😀'` |
| `PadLeft('abc', -1)` | `''` |
| `PadLeft('abc', null)` | `''` |
| `PadLeft(null, 5)` | `null` |

## PadLeft(value, count, symbol)

`PadLeft(value, count, symbol)` дополняет строку слева указанным символом или строкой.

Если `symbol` состоит из нескольких символов, он повторяется как строка, а затем результат обрезается до нужной длины.

Если `symbol` пустой, строка не дополняется.

Если `value` или `symbol` равен `null`, результат `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `PadLeft('abc', 5, '*')` | `'**abc'` |
| `PadLeft('abc', 5, '0')` | `'00abc'` |
| `PadLeft('abc', 3, '*')` | `'abc'` |
| `PadLeft('abc', 2, '*')` | `'ab'` |
| `PadLeft('abc', 0, '*')` | `''` |
| `PadLeft('', 5, '-')` | `'-----'` |
| `PadLeft('123', 5, '0')` | `'00123'` |
| `PadLeft('abc', 5, 'XY')` | `'XYabc'` |
| `PadLeft('abc', 5, '')` | `'abc'` |
| `PadLeft('abc', -1, '*')` | `''` |
| `PadLeft('abc', null, '*')` | `''` |
| `PadLeft('abc', 5, null)` | `null` |
| `PadLeft(null, 5, '*')` | `null` |

## PadRight(value, count)

`PadRight(value, count)` дополняет строку справа обычными пробелами.

Если `value` равен `null`, результат тоже `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `PadRight('abc', 5)` | `'abc  '` |
| `PadRight('abc', 3)` | `'abc'` |
| `PadRight('abc', 2)` | `'ab'` |
| `PadRight('abc', 0)` | `''` |
| `PadRight('', 5)` | `'     '` |
| `PadRight('привет', 8)` | `'привет  '` |
| `PadRight('😀', 3)` | `'😀  '` |
| `PadRight('abc', -1)` | `''` |
| `PadRight('abc', null)` | `''` |
| `PadRight(null, 5)` | `null` |

## PadRight(value, count, symbol)

`PadRight(value, count, symbol)` дополняет строку справа указанным символом или строкой.

Если `symbol` состоит из нескольких символов, он повторяется как строка, а затем результат обрезается до нужной длины.

Если `symbol` пустой, строка не дополняется.

Если `value` или `symbol` равен `null`, результат `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `PadRight('abc', 5, '*')` | `'abc**'` |
| `PadRight('abc', 5, '0')` | `'abc00'` |
| `PadRight('abc', 3, '*')` | `'abc'` |
| `PadRight('abc', 2, '*')` | `'ab'` |
| `PadRight('abc', 0, '*')` | `''` |
| `PadRight('', 5, '-')` | `'-----'` |
| `PadRight('123', 5, '0')` | `'12300'` |
| `PadRight('abc', 5, 'XY')` | `'abcXY'` |
| `PadRight('abc', 5, '')` | `'abc'` |
| `PadRight('abc', -1, '*')` | `''` |
| `PadRight('abc', null, '*')` | `''` |
| `PadRight('abc', 5, null)` | `null` |
| `PadRight(null, 5, '*')` | `null` |

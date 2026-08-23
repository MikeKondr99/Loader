# Trim

`Trim`, `TrimLeft` и `TrimRight` удаляют обычные пробелы по краям строки.

Внутренние пробелы не меняются. Табуляция, перенос строки и другие whitespace-символы сейчас не считаются пробелами для этих функций и остаются в строке.

TODO: добавить поддержку удаления других whitespace-символов по краям строки.

## Trim(value)

`Trim(value)` удаляет обычные пробелы с обеих сторон строки.

Если вход равен `null`, результат тоже `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `Trim('  hello  ')` | `'hello'` |
| `Trim('  ')` | `''` |
| `Trim('')` | `''` |
| `Trim('привет  ')` | `'привет'` |
| `Trim('😀  👍  ')` | `'😀  👍'` |
| `Trim('\thello\t')` | `'\thello\t'` |
| `Trim('\nhello\n')` | `'\nhello\n'` |
| `Trim(null)` | `null` |

## TrimLeft(value)

`TrimLeft(value)` удаляет обычные пробелы только в начале строки.

Если вход равен `null`, результат тоже `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `TrimLeft('  hello  ')` | `'hello  '` |
| `TrimLeft('  ')` | `''` |
| `TrimLeft('')` | `''` |
| `TrimLeft('  привет')` | `'привет'` |
| `TrimLeft('  😀👍')` | `'😀👍'` |
| `TrimLeft('\thello\t')` | `'\thello\t'` |
| `TrimLeft('\nhello\n')` | `'\nhello\n'` |
| `TrimLeft(null)` | `null` |

## TrimRight(value)

`TrimRight(value)` удаляет обычные пробелы только в конце строки.

Если вход равен `null`, результат тоже `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `TrimRight('  hello  ')` | `'  hello'` |
| `TrimRight('  ')` | `''` |
| `TrimRight('')` | `''` |
| `TrimRight('привет  ')` | `'привет'` |
| `TrimRight('😀👍  ')` | `'😀👍'` |
| `TrimRight('\thello\t')` | `'\thello\t'` |
| `TrimRight('\nhello\n')` | `'\nhello\n'` |
| `TrimRight(null)` | `null` |

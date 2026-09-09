# Trim

`Trim`, `LTrim` и `RTrim` удаляют обычные пробелы по краям строки.

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

## LTrim(value)

`LTrim(value)` удаляет обычные пробелы только в начале строки.

Если вход равен `null`, результат тоже `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `LTrim('  hello  ')` | `'hello  '` |
| `LTrim('  ')` | `''` |
| `LTrim('')` | `''` |
| `LTrim('  привет')` | `'привет'` |
| `LTrim('  😀👍')` | `'😀👍'` |
| `LTrim('\thello\t')` | `'\thello\t'` |
| `LTrim('\nhello\n')` | `'\nhello\n'` |
| `LTrim(null)` | `null` |

## RTrim(value)

`RTrim(value)` удаляет обычные пробелы только в конце строки.

Если вход равен `null`, результат тоже `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `RTrim('  hello  ')` | `'  hello'` |
| `RTrim('  ')` | `''` |
| `RTrim('')` | `''` |
| `RTrim('привет  ')` | `'привет'` |
| `RTrim('😀👍  ')` | `'😀👍'` |
| `RTrim('\thello\t')` | `'\thello\t'` |
| `RTrim('\nhello\n')` | `'\nhello\n'` |
| `RTrim(null)` | `null` |

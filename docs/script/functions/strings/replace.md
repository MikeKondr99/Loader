# Replace

`Replace` заменяет все вхождения одной строки на другую.

## Replace(value, from, to)

`Replace(value, from, to)` заменяет все точные текстовые вхождения `from` в строке `value` на `to`.

`from` трактуется как обычный текст, не как regular expression.

Если любой аргумент равен `null`, результат `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `Replace('hello', 'l', 'x')` | `'hexxo'` |
| `Replace('hello', 'ell', 'ipp')` | `'hippo'` |
| `Replace('a.*b', '.*', '-')` | `'a-b'` |
| `Replace('a\\d+b', '\\d+', '-')` | `'a-b'` |
| `Replace('a[b]', '[b]', 'c')` | `'ac'` |
| `Replace('^$', '^', '!')` | `'!$'` |
| `Replace('', 'x', 'y')` | `''` |
| `Replace('hello', 'x', 'y')` | `'hello'` |
| `Replace(null, 'a', 'b')` | `null` |
| `Replace('hello', null, 'x')` | `null` |
| `Replace('hello', 'l', null)` | `null` |

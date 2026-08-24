# ExcludeChars

`ExcludeChars` удаляет из строки все символы, перечисленные в charset.

## ExcludeChars(value, charset)

`ExcludeChars(value, charset)` возвращает строку без символов из `charset`.

`charset` трактуется как набор отдельных символов, а не как regular expression. Специальные regex-символы экранируются.

Если `charset` пустой, строка возвращается без изменений.

Если `value` или `charset` равен `null`, результат `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `ExcludeChars('a1b2c3', '123')` | `'abc'` |
| `ExcludeChars('hello world', 'aeiou')` | `'hll wrld'` |
| `ExcludeChars('abc123', '')` | `'abc123'` |
| `ExcludeChars('', '123')` | `''` |
| `ExcludeChars('aaaa', 'a')` | `''` |
| `ExcludeChars('abc', 'xyz')` | `'abc'` |
| `ExcludeChars('a b c', ' ')` | `'abc'` |
| `ExcludeChars('a-b]c^d', ']-^')` | `'abcd'` |
| `ExcludeChars('a.b*c+d?', '.*+?')` | `'abcd'` |
| `ExcludeChars('привет123', '123')` | `'привет'` |
| `ExcludeChars(null, '123')` | `null` |
| `ExcludeChars('abc', null)` | `null` |

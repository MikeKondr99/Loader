# KeepChars

`KeepChars` оставляет в строке только символы, перечисленные в charset.

## KeepChars(value, charset)

`KeepChars(value, charset)` удаляет все символы, которых нет в `charset`.

`charset` трактуется как набор отдельных символов, а не как regular expression. Специальные regex-символы экранируются.

Если `charset` пустой, результат пустая строка.

Если `value` или `charset` равен `null`, результат `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `KeepChars('a1b2c3', '123')` | `'123'` |
| `KeepChars('hello world', 'aeiou')` | `'eoo'` |
| `KeepChars('abc123', '')` | `''` |
| `KeepChars('', '123')` | `''` |
| `KeepChars('aaaa', 'a')` | `'aaaa'` |
| `KeepChars('abc', 'xyz')` | `''` |
| `KeepChars('a b c', 'abc')` | `'abc'` |
| `KeepChars('1a2b3c', '1234567890')` | `'123'` |
| `KeepChars('a-b]c^d', ']-^')` | `'-]^'` |
| `KeepChars('a.b*c+d?', '.*+?')` | `'.*+?'` |
| `KeepChars('привет123', '123')` | `'123'` |
| `KeepChars(null, '123')` | `null` |
| `KeepChars('abc', null)` | `null` |

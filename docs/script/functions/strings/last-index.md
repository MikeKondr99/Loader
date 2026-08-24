# LastIndex

`LastIndex` возвращает позицию последнего вхождения подстроки.

## LastIndex(value, substring)

`LastIndex(value, substring)` возвращает позицию последнего вхождения `substring` в `value`.

Позиция начинается с `1`.

Если подстрока не найдена, результат `null`.

Если `substring` пустая строка, результат равен длине строки плюс `1`.

Если `value` или `substring` равен `null`, результат `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `LastIndex('abc', 'a')` | `1` |
| `LastIndex('abcba', 'b')` | `4` |
| `LastIndex('abc', 'c')` | `3` |
| `LastIndex('abcabc', 'bc')` | `5` |
| `LastIndex('abc', 'd')` | `null` |
| `LastIndex('', 'a')` | `null` |
| `LastIndex('abc', '')` | `4` |
| `LastIndex('aabaa', 'aa')` | `4` |
| `LastIndex('привет', 'е')` | `5` |
| `LastIndex(null, 'a')` | `null` |
| `LastIndex('abc', null)` | `null` |

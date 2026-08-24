# Repeat

`Repeat` повторяет строку указанное количество раз.

## Repeat(value, count)

`Repeat(value, count)` возвращает `value`, повторенный `count` раз.

Если `count = 0` или `count < 0`, результат пустая строка.

Если `value` или `count` равен `null`, результат `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `Repeat('a', 3)` | `'aaa'` |
| `Repeat('ab', 2)` | `'abab'` |
| `Repeat(' ', 4)` | `'    '` |
| `Repeat('', 5)` | `''` |
| `Repeat('a', 0)` | `''` |
| `Repeat('a', 1)` | `'a'` |
| `Repeat('привет', 2)` | `'приветпривет'` |
| `Repeat('😀', 3)` | `'😀😀😀'` |
| `Repeat('\\n', 2)` | `'\\n\\n'` |
| `Repeat('.*+', 2)` | `'.*+.*+'` |
| `Repeat('a', -1)` | `''` |
| `Repeat('abc', 1000).Len()` | `3000` |
| `Repeat(null, 3)` | `null` |
| `Repeat('abc', null)` | `null` |

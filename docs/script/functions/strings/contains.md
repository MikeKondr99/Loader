# Contains

`Contains` проверяет, содержит ли строка подстроку.

## Contains(value, substring)

`Contains(value, substring)` возвращает `true`, если `value` содержит `substring`.

Проверка case-sensitive.

Пустая подстрока считается найденной.

Если `value` или `substring` равен `null`, результат `null`.

Примеры:

| Expression | Result |
| --- | --- |
| `'hello'.Contains('ell')` | `true` |
| `Contains('hello', 'world')` | `false` |
| `'hello'.Contains('')` | `true` |
| `''.Contains('a')` | `false` |
| `''.Contains('')` | `true` |
| `Contains('привет', 'иве')` | `true` |
| `'😀👍👋'.Contains('👍')` | `true` |
| `'Apple'.Contains('P')` | `false` |
| `'Apple'.Contains('p')` | `true` |
| `Contains(null, 'a')` | `null` |
| `Contains('abc', null)` | `null` |
